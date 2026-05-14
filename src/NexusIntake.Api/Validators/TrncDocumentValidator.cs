using NexusIntake.Api.Models;

namespace NexusIntake.Api.Validators;

public static partial class TrncDocumentValidator
{
    private const double MinimumConfidence = 0.6;

    public static List<string> Validate(CustomerLead lead)
    {
        var errors = new List<string>();

        if (lead.ConfidenceScore < MinimumConfidence)
        {
            errors.Add("Guven skoru cok dusuk — goruntu bulanik veya okunamaz durumda");
            return errors;
        }

        if (lead.DocumentType == DocumentType.Kimlik)
        {
            if (string.IsNullOrWhiteSpace(lead.IdNumber))
                errors.Add("Kimlik numarasi zorunludur");
            else if (lead.IdNumber.Length is < 5 or > 30)
                errors.Add("Gecersiz kimlik numarasi: en az 5, en fazla 30 karakter olmalidir");

            if (string.IsNullOrWhiteSpace(lead.Name))
                errors.Add("Ad alani zorunludur");

            if (string.IsNullOrWhiteSpace(lead.Surname))
                errors.Add("Soyad alani zorunludur");

            if (lead.DateOfBirth is null || lead.DateOfBirth > DateTime.UtcNow)
                errors.Add("Gecersiz dogum tarihi");

            if (string.IsNullOrWhiteSpace(lead.Nationality))
                errors.Add("Uyruk alani zorunludur");

            if (string.IsNullOrWhiteSpace(lead.Gender))
                errors.Add("Cinsiyet alani zorunludur");
        }
        else if (lead.DocumentType == DocumentType.InsurancePolicy)
        {
            if (string.IsNullOrWhiteSpace(lead.PolicyNumber))
                errors.Add("Polis numarasi zorunludur");

            if (string.IsNullOrWhiteSpace(lead.VehiclePlate))
                errors.Add("Arac plakasi zorunludur");

            if (lead.PolicyExpiry is null || lead.PolicyExpiry <= DateTime.UtcNow)
                errors.Add("Polis suresi dolmus veya gecersiz tarih");
        }
        else
        {
            errors.Add("Bilinmeyen belge turu — siniflandirilamadi");
        }

        return errors;
    }

    public static string NormalizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return CultureInfoNormalizer.ToTitleCase(input.Trim().ToLowerInvariant());
    }

    public static string NormalizeTurkishText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return input.Trim().ToUpperInvariant();
    }
}

internal static class CultureInfoNormalizer
{
    public static string ToTitleCase(string input)
    {
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input);
    }
}
