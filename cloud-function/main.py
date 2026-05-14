"""
Nexus Intake — TRNC Document Extraction Cloud Function
Triggered via HTTP. Expects JSON: {"gcs_uri": "gs://bucket/object"}
Responds with structured JSON from Gemini 1.5 Flash vision analysis.
"""
import json
import logging
import os
from datetime import datetime, timezone

import functions_framework
from google.cloud import storage, aiplatform
import vertexai
from vertexai.generative_models import GenerativeModel, Part

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

PROJECT_ID = os.environ.get("GCP_PROJECT", "nexus-intake")
LOCATION = os.environ.get("GCP_LOCATION", "us-central1")
MODEL_NAME = "gemini-2.5-flash"

vertexai.init(project=PROJECT_ID, location=LOCATION)

SYSTEM_PROMPT = """Sen bir uluslararasi kimlik ve sigorta belgesi cozumleyicisisin.
Gorevin resmi belge gorsellerinden veri cikarmaktir. Her ulkeden kimlik karti kabul edilir.

Kurallar:
1. Eger gorsel bir Kimlik karti ise sunlari cikar: Ad, Soyad, Kimlik No (belgede yazan haliyle), Dogum Tarihi, Kimlik Son Kullanma Tarihi, Uyruk (belgede yazan ulke), Cinsiyet.
2. Eger gorsel bir Sigorta Policesi ise sunlari cikar: Polis No, Arac Plaka, Prim (TL cinsinden), Polis Son Kullanma Tarihi.
3. Tum alan isimleri Turkce OLMALIDIR.
4. Eger gorsel cok bulanik veya okunamaz durumda ise, hata alanina uygun bir mesaj yaz.
5. Yalnizca gecerli JSON ciktisi ver. Markdown veya ekstra metin olmasin.

Cikti JSON semasi:
{
  "belge_turu": "kimlik" | "sigorta_policesi" | "bilinmeyen",
  "ad": "string veya null",
  "soyad": "string veya null",
  "kimlik_no": "string veya null (belgede yazan haliyle)",
  "dogum_tarihi": "YYYY-MM-DD veya null",
  "kimlik_son_kullanma": "YYYY-MM-DD veya null",
  "uyruk": "string veya null (ornek: KKTC, TC, UK, DE, vb.)",
  "cinsiyet": "string veya null (ERKEK veya KADIN)",
  "polis_no": "string veya null",
  "arac_plaka": "string veya null",
  "prim": "string veya null (sayisal, TL)",
  "polis_son_kullanma": "YYYY-MM-DD veya null",
  "guven_skoru": 0.0-1.0,
  "hata": "string veya null (goruntu kullanilamaz durumda ise)"
}
"""


def download_from_gcs(gcs_uri: str) -> bytes:
    """Download file bytes from a GCS URI."""
    if not gcs_uri.startswith("gs://"):
        raise ValueError(f"Invalid GCS URI: {gcs_uri}")

    path = gcs_uri[5:]  # strip gs://
    bucket_name, object_name = path.split("/", 1)

    client = storage.Client()
    bucket = client.bucket(bucket_name)
    blob = bucket.blob(object_name)
    return blob.download_as_bytes()


def _extract_json(text: str) -> str:
    """Extract JSON string from Gemini response — handles markdown fences and inline text."""
    import re

    # Try to find JSON inside ```json ... ``` or ``` ... ``` fences
    fence_pattern = r'```(?:json)?\s*\n(.*?)\n```'
    matches = re.findall(fence_pattern, text, re.DOTALL)
    for match in matches:
        stripped = match.strip()
        if stripped.startswith("{"):
            return stripped

    # No fences found — find the first { ... } block
    start = text.find("{")
    if start == -1:
        return text

    # Track braces to find the matching closing brace
    depth = 0
    for i, char in enumerate(text[start:], start=start):
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return text[start:i + 1]

    return text


def extract_with_gemini(image_bytes: bytes) -> dict:
    """Send image to Gemini 1.5 Flash and parse the JSON response."""
    model = GenerativeModel(
        model_name=MODEL_NAME,
        system_instruction=SYSTEM_PROMPT,
    )

    image_part = Part.from_data(
        data=image_bytes,
        mime_type="image/jpeg",
    )

    response = model.generate_content(
        contents=[image_part],
        generation_config={
            "temperature": 0.1,
            "max_output_tokens": 1024,
            "top_p": 0.95,
        },
    )

    raw_text = response.text.strip() if response.text else ""

    # Try to extract JSON from markdown code fences anywhere in the response
    json_str = _extract_json(raw_text)

    try:
        result = json.loads(json_str)
    except (json.JSONDecodeError, ValueError):
        logger.warning(f"Gemini returned non-JSON: {raw_text[:500]}")
        result = {
            "belge_turu": "bilinmeyen",
            "guven_skoru": 0.0,
            "hata": "AI yaniti gecerli JSON degil — goruntu okunamaz durumda olabilir",
        }

    # Migrate old English keys to Turkish keys (Gemini sometimes ignores prompt language)
    key_migration = {
        "document_type": "belge_turu",
        "name": "ad",
        "surname": "soyad",
        "id_number": "kimlik_no",
        "date_of_birth": "dogum_tarihi",
        "id_expiry": "kimlik_son_kullanma",
        "nationality": "uyruk",
        "gender": "cinsiyet",
        "policy_number": "polis_no",
        "vehicle_plate": "arac_plaka",
        "premium": "prim",
        "policy_expiry": "polis_son_kullanma",
        "confidence_score": "guven_skoru",
        "error": "hata",
    }
    for en_key, tr_key in key_migration.items():
        if en_key in result and tr_key not in result:
            result[tr_key] = result.pop(en_key)

    # Also migrate document_type value: "unknown" → "bilinmeyen"
    if result.get("belge_turu") == "unknown":
        result["belge_turu"] = "bilinmeyen"
    if result.get("belge_turu") == "insurance_policy":
        result["belge_turu"] = "sigorta_policesi"

    # Ensure required fields exist with defaults
    required_fields = [
        "belge_turu", "ad", "soyad", "kimlik_no", "dogum_tarihi",
        "kimlik_son_kullanma", "uyruk", "cinsiyet",
        "polis_no", "arac_plaka", "prim",
        "polis_son_kullanma", "guven_skoru", "hata"
    ]
    for field in required_fields:
        if field not in result:
            result[field] = None

    # Normalize date fields: Gemini sometimes gives Turkish month names or DD/MM/YYYY
    for date_field in ["dogum_tarihi", "kimlik_son_kullanma", "polis_son_kullanma"]:
        raw = result.get(date_field)
        if raw and isinstance(raw, str):
            result[date_field] = normalize_date(raw)

    return result


def normalize_date(raw: str) -> str | None:
    """Attempt to normalize various date formats to YYYY-MM-DD."""
    if not raw or not isinstance(raw, str):
        return None

    # Already correct format
    if len(raw) == 10 and raw[4] == "-" and raw[7] == "-":
        return raw

    # Turkish month name map
    tr_months = {
        "ocak": "01", "şubat": "02", "subat": "02", "mart": "03",
        "nisan": "04", "mayıs": "05", "mayis": "05", "haziran": "06",
        "temmuz": "07", "ağustos": "08", "agustos": "08",
        "eylül": "09", "eylul": "09", "ekim": "10", "kasım": "11",
        "kasim": "11", "aralık": "12", "aralik": "12",
    }

    try:
        lower = raw.lower().strip()
        for tr_month, num in tr_months.items():
            if tr_month in lower:
                parts = lower.replace(",", "").replace(".", "").split()
                day = next((p for p in parts if p.isdigit() and 1 <= int(p) <= 31), None)
                year = next(
                    (p for p in parts if p.isdigit() and (len(p) == 4 or (len(p) == 2 and 50 <= int(p) <= 99))),
                    None
                )
                if year and day:
                    if len(year) == 2:
                        year = f"20{year}"
                    return f"{year}-{num}-{int(day):02d}"
                break

        # Try DD.MM.YYYY or DD/MM/YYYY
        for sep in [".", "/", "-"]:
            parts = raw.split(sep)
            if len(parts) == 3 and all(p.strip().isdigit() for p in parts):
                d, m, y = parts
                if len(y) == 2:
                    y = f"20{y}"
                if 1 <= int(d) <= 31 and 1 <= int(m) <= 12:
                    return f"{int(y):04d}-{int(m):02d}-{int(d):02d}"

    except (ValueError, IndexError):
        pass

    return raw


@functions_framework.http
def extract_document(request):
    """HTTP Cloud Function entry point."""
    # CORS preflight
    if request.method == "OPTIONS":
        headers = {
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Methods": "POST, OPTIONS",
            "Access-Control-Allow-Headers": "Content-Type",
        }
        return ("", 204, headers)

    headers = {"Access-Control-Allow-Origin": "*", "Content-Type": "application/json"}

    try:
        body = request.get_json(silent=True)
        if not body or "gcs_uri" not in body:
            error_response = json.dumps({
                "belge_turu": "bilinmeyen",
                "guven_skoru": 0.0,
                "hata": "Istek govdesinde 'gcs_uri' eksik",
            })
            return (error_response, 400, headers)

        gcs_uri = body["gcs_uri"]
        logger.info(f"Processing: {gcs_uri}")

        image_bytes = download_from_gcs(gcs_uri)
        result = extract_with_gemini(image_bytes)

        result["_gcs_uri"] = gcs_uri
        result["_processed_at"] = datetime.now(timezone.utc).isoformat()

        # If low confidence, flag as blurry
        confidence = result.get("guven_skoru", 0.0)
        if isinstance(confidence, (int, float)) and confidence < 0.4 and not result.get("hata"):
            result["hata"] = "Goruntu kalitesi guvenilir cozumleme icin cok dusuk"

        logger.info(f"Extraction complete: belge_turu={result.get('belge_turu')}, "
                     f"guven_skoru={confidence}")

        return (json.dumps(result, ensure_ascii=False), 200, headers)

    except Exception as e:
        logger.exception("Extraction failed")
        error_response = json.dumps({
            "belge_turu": "bilinmeyen",
            "guven_skoru": 0.0,
            "hata": str(e),
        })
        return (error_response, 500, headers)
