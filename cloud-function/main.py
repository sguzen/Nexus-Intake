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

SYSTEM_PROMPT = """You are a specialized TRNC (Turkish Republic of Northern Cyprus) Document Parser.
Your task is to extract data from images of official documents.

Rules:
1. If the image is a TRNC Kimlik (ID Card), extract: Name, Surname, ID Number (must be 10 digits), Date of Birth, ID Expiry Date.
2. If the image is an Insurance Policy (Turkish: Sigorta Poliçesi), extract: Policy Number, Vehicle Plate, Premium (in TRY), Policy Expiry Date.
3. Translate all Turkish field name keys to their English equivalents.
4. If the image is too blurry or unreadable, set error field with an appropriate message.
5. Output strictly valid JSON with the schema below. No markdown, no extra text.

Output JSON schema:
{
  "document_type": "kimlik" | "insurance_policy" | "unknown",
  "name": "string or null",
  "surname": "string or null",
  "id_number": "string or null (10 digits if kimlik)",
  "date_of_birth": "YYYY-MM-DD or null",
  "id_expiry": "YYYY-MM-DD or null",
  "policy_number": "string or null",
  "vehicle_plate": "string or null",
  "premium": "string or null (numeric, TRY)",
  "policy_expiry": "YYYY-MM-DD or null",
  "confidence_score": 0.0-1.0,
  "error": "string or null (set if image is unusable)"
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

    # Strip markdown code fences if present
    if raw_text.startswith("```"):
        lines = raw_text.split("\n")
        lines = [l for l in lines if not l.startswith("```")]
        raw_text = "\n".join(lines).strip()

    try:
        result = json.loads(raw_text)
    except json.JSONDecodeError:
        logger.warning(f"Gemini returned non-JSON: {raw_text[:500]}")
        result = {
            "document_type": "unknown",
            "confidence_score": 0.0,
            "error": "AI response was not valid JSON — image may be unreadable",
        }

    # Ensure required fields exist with defaults
    required_fields = [
        "document_type", "name", "surname", "id_number", "date_of_birth",
        "id_expiry", "policy_number", "vehicle_plate", "premium",
        "policy_expiry", "confidence_score", "error"
    ]
    for field in required_fields:
        if field not in result:
            result[field] = None

    # Normalize date fields: Gemini sometimes gives Turkish month names or DD/MM/YYYY
    for date_field in ["date_of_birth", "id_expiry", "policy_expiry"]:
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
                "document_type": "unknown",
                "confidence_score": 0.0,
                "error": "Missing 'gcs_uri' in request body",
            })
            return (error_response, 400, headers)

        gcs_uri = body["gcs_uri"]
        logger.info(f"Processing: {gcs_uri}")

        image_bytes = download_from_gcs(gcs_uri)
        result = extract_with_gemini(image_bytes)

        result["_gcs_uri"] = gcs_uri
        result["_processed_at"] = datetime.now(timezone.utc).isoformat()

        # If low confidence, flag as blurry
        confidence = result.get("confidence_score", 0.0)
        if isinstance(confidence, (int, float)) and confidence < 0.4 and not result.get("error"):
            result["error"] = "Image quality too low for reliable extraction"

        logger.info(f"Extraction complete: doc_type={result.get('document_type')}, "
                     f"confidence={confidence}")

        return (json.dumps(result, ensure_ascii=False), 200, headers)

    except Exception as e:
        logger.exception("Extraction failed")
        error_response = json.dumps({
            "document_type": "unknown",
            "confidence_score": 0.0,
            "error": str(e),
        })
        return (error_response, 500, headers)
