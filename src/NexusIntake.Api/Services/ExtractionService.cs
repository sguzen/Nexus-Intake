using System.Text.Json;
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
        var result = JsonSerializer.Deserialize<ExtractionResult>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new InvalidOperationException("Extraction service returned null");
    }
}
