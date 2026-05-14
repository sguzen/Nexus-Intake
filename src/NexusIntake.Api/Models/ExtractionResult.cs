using System.Text.Json.Serialization;

namespace NexusIntake.Api.Models;

public class ExtractionResult
{
    [JsonPropertyName("belge_turu")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("ad")]
    public string? Name { get; set; }

    [JsonPropertyName("soyad")]
    public string? Surname { get; set; }

    [JsonPropertyName("kimlik_no")]
    public string? IdNumber { get; set; }

    [JsonPropertyName("dogum_tarihi")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("kimlik_son_kullanma")]
    public string? IdExpiry { get; set; }

    [JsonPropertyName("uyruk")]
    public string? Nationality { get; set; }

    [JsonPropertyName("cinsiyet")]
    public string? Gender { get; set; }

    [JsonPropertyName("polis_no")]
    public string? PolicyNumber { get; set; }

    [JsonPropertyName("arac_plaka")]
    public string? VehiclePlate { get; set; }

    [JsonPropertyName("prim")]
    public string? Premium { get; set; }

    [JsonPropertyName("polis_son_kullanma")]
    public string? PolicyExpiry { get; set; }

    [JsonPropertyName("guven_skoru")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("hata")]
    public string? Error { get; set; }
}
