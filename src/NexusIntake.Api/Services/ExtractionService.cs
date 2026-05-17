using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly ILogger<ExtractionService> _logger;

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

    public ExtractionService(HttpClient httpClient, IConfiguration configuration, ILogger<ExtractionService> logger)
    {
        _httpClient = httpClient;
        _extractorUrl = configuration["Extraction:CloudFunctionUrl"]
            ?? throw new InvalidOperationException("Extraction:CloudFunctionUrl is not configured");
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(string gcsUri, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { gcs_uri = gcsUri });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        // Attach Google OIDC token to authenticate with the secured Cloud Function
        var oidcToken = await GetOidcTokenAsync(ct);
        if (!string.IsNullOrWhiteSpace(oidcToken))
        {
            content.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oidcToken);
        }

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

    private static async Task<string?> GetOidcTokenAsync(CancellationToken ct)
    {
        try
        {
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefault()
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

            if (credential is ITokenAccess tokenAccess)
            {
                var accessToken = await tokenAccess.GetAccessTokenForRequestAsync();
                return accessToken;
            }
        }
        catch (Exception ex)
        {
            // If we can't get a token (e.g., local dev), the request will fail if the function is locked.
            // Log and continue — the caller will get a 401, which is correct for production.
        }
        return null;
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