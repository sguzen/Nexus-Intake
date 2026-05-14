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
        sb.AppendLine("`[SISTEM: TARAMA PROTOKOLU BASLATILIYOR...]`");
        sb.AppendLine("`[SISTEM: BELGE COZULUYOR...]`");
        return sb.ToString();
    }

    public static string ProcessingComplete()
    {
        return "`[SISTEM: COZUMLEME TAMAMLANDI ✓]`";
    }

    public static string ErrorBlurryImage()
    {
        return "`[HATA: SINYAL GURULTUSU COK YUKSEK. BELGEYI TEKRAR TARAYIN.]`";
    }

    public static string Error(string message)
    {
        return $"`[HATA]` {EscapeMarkdown(message)}";
    }

    public static string FormatKimlikResult(CustomerLead lead)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< KIMLIK COZUMLEMESI >>`*");
        sb.AppendLine();
        sb.AppendLine($"`AD:` {EscapeMarkdown(lead.Name ?? "YOK")}");
        sb.AppendLine($"`SOYAD:` {EscapeMarkdown(lead.Surname ?? "YOK")}");
        sb.AppendLine($"`TC/KIMLIK NO:` {EscapeMarkdown(lead.IdNumber ?? "YOK")}");
        sb.AppendLine($"`DOGUM TARIHI:` {EscapeMarkdown(lead.DateOfBirth?.ToString("dd.MM.yyyy") ?? "YOK")}");
        sb.AppendLine($"`UYRUK:` {EscapeMarkdown(lead.Nationality ?? "YOK")}");
        sb.AppendLine($"`CINSIYET:` {EscapeMarkdown(lead.Gender ?? "YOK")}");
        sb.AppendLine($"`GECERLILIK:` {EscapeMarkdown(lead.IdExpiry?.ToString("dd.MM.yyyy") ?? "YOK")}");
        sb.AppendLine($"`GUVEN SKORU:` {EscapeMarkdown($"{(lead.ConfidenceScore * 100):F1}%")}");
        sb.AppendLine();
        sb.AppendLine("`[DURUM: MUSTERI KAYDI OLUSTURULDU ✓]`");
        return sb.ToString();
    }

    public static string FormatPolicyResult(CustomerLead lead)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< POLIS COZUMLEMESI >>`*");
        sb.AppendLine();
        sb.AppendLine($"`POLIÇE NO:` {EscapeMarkdown(lead.PolicyNumber ?? "YOK")}");
        sb.AppendLine($"`PLAKA:` {EscapeMarkdown(lead.VehiclePlate ?? "YOK")}");
        sb.AppendLine($"`PRIM:` {EscapeMarkdown(lead.Premium?.ToString("N2") ?? "YOK")} TL");
        sb.AppendLine($"`GECERLILIK:` {EscapeMarkdown(lead.PolicyExpiry?.ToString("dd.MM.yyyy") ?? "YOK")}");
        sb.AppendLine($"`GUVEN SKORU:` {EscapeMarkdown($"{(lead.ConfidenceScore * 100):F1}%")}");
        sb.AppendLine();
        sb.AppendLine("`[DURUM: POLIÇE SISTEME EKLENDI ✓]`");
        return sb.ToString();
    }

    public static string FormatValidationErrors(List<string> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("*`<< DOGRULAMA RAPORU >>`*");
        sb.AppendLine();
        foreach (var err in errors)
        {
            sb.AppendLine($"`>>` {EscapeMarkdown(err)}");
        }
        return sb.ToString();
    }
}
