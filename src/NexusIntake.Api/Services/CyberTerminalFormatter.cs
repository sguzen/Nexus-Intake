using System.Text;
using NexusIntake.Api.Models;

namespace NexusIntake.Api.Services;

public static class CyberTerminalFormatter
{
    private static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length * 2);
        foreach (var ch in text)
        {
            if (ch is '_' or '*' or '[' or ']' or '(' or ')' or '~' or '`' or '>' or '#'
                or '+' or '-' or '=' or '|' or '{' or '}' or '.' or '!')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    public static string SystemLog(string message)
    {
        return $"`[SYS]` {EscapeMarkdown(message)}";
    }

    public static string ProcessingStart()
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< NEXUS INTAKE v1.0 >>`*");
        sb.AppendLine();
        sb.AppendLine("`[SYSTEM: INITIALIZING SCAN PROTOCOL...]`");
        sb.AppendLine("`[SYSTEM: DECRYPTING DOCUMENT...]`");
        return sb.ToString();
    }

    public static string ProcessingComplete()
    {
        return "`[SYSTEM: EXTRACTION COMPLETE ✓]`";
    }

    public static string ErrorBlurryImage()
    {
        return "`[ERROR: SIGNAL NOISE TOO HIGH. RE-SCAN DOCUMENT.]`";
    }

    public static string Error(string message)
    {
        return $"`[ERROR]` {EscapeMarkdown(message)}";
    }

    public static string FormatKimlikResult(CustomerLead lead)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< KIMLIK EXTRACTION >>`*");
        sb.AppendLine();
        sb.AppendLine($"`NAME:` {EscapeMarkdown(lead.Name ?? "N/A")}");
        sb.AppendLine($"`SURNAME:` {EscapeMarkdown(lead.Surname ?? "N/A")}");
        sb.AppendLine($"`ID NO:` {EscapeMarkdown(lead.IdNumber ?? "N/A")}");
        sb.AppendLine($"`DOB:` {EscapeMarkdown(lead.DateOfBirth?.ToString("dd.MM.yyyy") ?? "N/A")}");
        sb.AppendLine($"`EXPIRY:` {EscapeMarkdown(lead.IdExpiry?.ToString("dd.MM.yyyy") ?? "N/A")}");
        sb.AppendLine($"`CONFIDENCE:` {EscapeMarkdown($"{(lead.ConfidenceScore * 100):F1}%")}");
        sb.AppendLine();
        sb.AppendLine("`[STATUS: LEAD REGISTERED ✓]`");
        return sb.ToString();
    }

    public static string FormatPolicyResult(CustomerLead lead)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< POLICY EXTRACTION >>`*");
        sb.AppendLine();
        sb.AppendLine($"`POLICY NO:` {EscapeMarkdown(lead.PolicyNumber ?? "N/A")}");
        sb.AppendLine($"`PLATE:` {EscapeMarkdown(lead.VehiclePlate ?? "N/A")}");
        sb.AppendLine($"`PREMIUM:` {EscapeMarkdown(lead.Premium?.ToString("N2") ?? "N/A")} TRY");
        sb.AppendLine($"`EXPIRY:` {EscapeMarkdown(lead.PolicyExpiry?.ToString("dd.MM.yyyy") ?? "N/A")}");
        sb.AppendLine($"`CONFIDENCE:` {EscapeMarkdown($"{(lead.ConfidenceScore * 100):F1}%")}");
        sb.AppendLine();
        sb.AppendLine("`[STATUS: POLICY INGESTED ✓]`");
        return sb.ToString();
    }

    public static string FormatValidationErrors(List<string> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< VALIDATION REPORT >>`*");
        sb.AppendLine();
        foreach (var err in errors)
        {
            sb.AppendLine($"`>>` {EscapeMarkdown(err)}");
        }
        return sb.ToString();
    }
}
