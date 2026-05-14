using System.Text.Json;
using System.Text.RegularExpressions;
using NexusIntake.Api.Models;

namespace NexusIntake.Api.Services;

public interface IExtractionService
{
    Task<ExtractionResult> ExtractAsync(string gcsUri, CancellationToken ct = default);
}

public class ExtractionService : IExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly string _extractorUrl;

    private static readonly Dictionary<string, string> KeyMap = new()
    {
        ["\"document_type\""] = "\"belge_turu\"",
        ["\"name\""] = "\"ad\"",
        ["\"surname\""] = "\"soyad\"",
        ["\"id_number\""] = "\"kimlik_no\"",
        ["\"date_of_birth\""] = "\"dogum_tarihi\"",
        ["\"id_expiry\""] = "\"kimlik_son_kullanma\"",
        ["\"nationality\""] = "\"uyruk\"",
        ["\"gender\""] = "\"cinsiyet\"",
        ["\"policy_number\""] = "\"polis_no\"",
        ["\"vehicle_plate\""] = "\"arac_plaka\"",
        ["\"premium\""] = "\"prim\"",
        ["\"policy_expiry\""] = "\"polis_son_kullanma\"",
        ["\"confidence_score\""] = "\"guven_skoru\"",
        ["\"error\""] = "\"hata\"",
        ["\"unknown\""] = "\"bilinmeyen\"",
        ["\"insurance_policy\""] = "\"sigorta_policesi\"",
    };

    public ExtractionService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _extractorUrl = configuration["Extraction:CloudFunctionUrl"]
            ?? throw new InvalidOperationException("Extraction:CloudFunctionUrl is not configured");
    }

    public async Task<ExtractionResult> ExtractAsync(string gcsUri, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { gcs_uri = gcsUri });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_extractorUrl, content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        json = NormalizeKeys(json);

        var result = JsonSerializer.Deserialize<ExtractionResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new InvalidOperationException("Extraction service returned null");
    }

    private static string NormalizeKeys(string json)
    {
        foreach (var (oldKey, newKey) in KeyMap)
        {
            json = json.Replace(oldKey, newKey);
        }
        return json;
    }
}
