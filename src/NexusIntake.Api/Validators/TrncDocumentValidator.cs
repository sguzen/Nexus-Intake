using System.Text.RegularExpressions;
using NexusIntake.Api.Models;

namespace NexusIntake.Api.Validators;

public static partial class TrncDocumentValidator
{
    private const int TrncIdLength = 10;
    private const double MinimumConfidence = 0.6;

    [GeneratedRegex(@"^\d{10}$")]
    private static partial Regex IdNumberPattern();

    public static List<string> Validate(CustomerLead lead)
    {
        var errors = new List<string>();

        if (lead.ConfidenceScore < MinimumConfidence)
        {
            errors.Add("Confidence score too low — image may be blurry or unreadable");
            return errors;
        }

        if (lead.DocumentType == DocumentType.Kimlik)
        {
            if (string.IsNullOrWhiteSpace(lead.IdNumber) || !IdNumberPattern().IsMatch(lead.IdNumber))
                errors.Add("Invalid TRNC ID number: must be exactly 10 digits");

            if (string.IsNullOrWhiteSpace(lead.Name))
                errors.Add("Name is required");

            if (string.IsNullOrWhiteSpace(lead.Surname))
                errors.Add("Surname is required");

            if (lead.DateOfBirth is null || lead.DateOfBirth > DateTime.UtcNow)
                errors.Add("Invalid date of birth");
        }
        else if (lead.DocumentType == DocumentType.InsurancePolicy)
        {
            if (string.IsNullOrWhiteSpace(lead.PolicyNumber))
                errors.Add("Policy number is required");

            if (string.IsNullOrWhiteSpace(lead.VehiclePlate))
                errors.Add("Vehicle plate is required");

            if (lead.PolicyExpiry is null || lead.PolicyExpiry <= DateTime.UtcNow)
                errors.Add("Policy has expired or expiry date is invalid");
        }
        else
        {
            errors.Add("Unknown document type — unable to classify");
        }

        return errors;
    }

    public static string NormalizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return CultureInfoNormalizer.ToTitleCase(input.Trim().ToLowerInvariant());
    }
}

internal static class CultureInfoNormalizer
{
    public static string ToTitleCase(string input)
    {
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input);
    }
}
