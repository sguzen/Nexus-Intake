using NexusIntake.Api.Models;
using NexusIntake.Api.Validators;

namespace NexusIntake.Api.Services;

public interface ICustomerLeadService
{
    (CustomerLead Lead, List<string> Errors) ProcessExtraction(ExtractionResult extraction, string gcsUri);
    bool Exists(string? idNumber, string? policyNumber);
}

public class CustomerLeadService : ICustomerLeadService
{
    // Mock in-memory store for deduplication
    private static readonly HashSet<string> _seenIdNumbers = new();
    private static readonly HashSet<string> _seenPolicyNumbers = new();

    public (CustomerLead Lead, List<string> Errors) ProcessExtraction(ExtractionResult extraction, string gcsUri)
    {
        var documentType = extraction.DocumentType?.ToLowerInvariant() switch
        {
            "kimlik" or "trnc_id" => DocumentType.Kimlik,
            "insurance_policy" or "policy" => DocumentType.InsurancePolicy,
            _ => DocumentType.Unknown
        };

        var lead = new CustomerLead
        {
            RawGcsUri = gcsUri,
            DocumentType = documentType,
            Name = TrncDocumentValidator.NormalizeName(extraction.Name),
            Surname = TrncDocumentValidator.NormalizeName(extraction.Surname),
            IdNumber = extraction.IdNumber?.Trim(),
            DateOfBirth = ParseDate(extraction.DateOfBirth),
            IdExpiry = ParseDate(extraction.IdExpiry),
            PolicyNumber = extraction.PolicyNumber?.Trim(),
            VehiclePlate = extraction.VehiclePlate?.Trim().ToUpperInvariant(),
            Premium = ParseDecimal(extraction.Premium),
            PolicyExpiry = ParseDate(extraction.PolicyExpiry),
            ConfidenceScore = extraction.ConfidenceScore
        };

        var errors = TrncDocumentValidator.Validate(lead);

        if (errors.Count == 0)
        {
            if (lead.DocumentType == DocumentType.Kimlik && !string.IsNullOrEmpty(lead.IdNumber))
            {
                if (_seenIdNumbers.Contains(lead.IdNumber))
                    errors.Add("Duplicate ID number detected");
                else
                    _seenIdNumbers.Add(lead.IdNumber);
            }
            else if (lead.DocumentType == DocumentType.InsurancePolicy && !string.IsNullOrEmpty(lead.PolicyNumber))
            {
                if (_seenPolicyNumbers.Contains(lead.PolicyNumber))
                    errors.Add("Duplicate policy number detected");
                else
                    _seenPolicyNumbers.Add(lead.PolicyNumber);
            }
        }

        return (lead, errors);
    }

    public bool Exists(string? idNumber, string? policyNumber)
    {
        if (!string.IsNullOrEmpty(idNumber) && _seenIdNumbers.Contains(idNumber))
            return true;
        if (!string.IsNullOrEmpty(policyNumber) && _seenPolicyNumbers.Contains(policyNumber))
            return true;
        return false;
    }

    private static DateTime? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return DateTime.TryParse(input, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt : null;
    }

    private static decimal? ParseDecimal(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return decimal.TryParse(input, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : null;
    }
}
