using System.Text.Json.Serialization;

namespace NexusIntake.Api.Models;

public class ExtractionResult
{
    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("id_number")]
    public string? IdNumber { get; set; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("id_expiry")]
    public string? IdExpiry { get; set; }

    [JsonPropertyName("policy_number")]
    public string? PolicyNumber { get; set; }

    [JsonPropertyName("vehicle_plate")]
    public string? VehiclePlate { get; set; }

    [JsonPropertyName("premium")]
    public string? Premium { get; set; }

    [JsonPropertyName("policy_expiry")]
    public string? PolicyExpiry { get; set; }

    [JsonPropertyName("confidence_score")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
