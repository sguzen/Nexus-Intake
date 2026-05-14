namespace NexusIntake.Api.Models;

public class CustomerLead
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public string? Name { get; init; }
    public string? Surname { get; init; }
    public string? IdNumber { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime? IdExpiry { get; init; }
    public string? Nationality { get; init; }
    public string? Gender { get; init; }

    public string? PolicyNumber { get; init; }
    public string? VehiclePlate { get; init; }
    public decimal? Premium { get; init; }
    public DateTime? PolicyExpiry { get; init; }

    public DocumentType DocumentType { get; init; }
    public double ConfidenceScore { get; init; }
    public string RawGcsUri { get; init; } = string.Empty;
}

public enum DocumentType
{
    Unknown,
    Kimlik,
    InsurancePolicy
}
