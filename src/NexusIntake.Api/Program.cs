using Telegram.Bot;
using Google.Cloud.Storage.V1;
using NexusIntake.Api.Services;
using NexusIntake.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var token = config["Telegram:BotToken"]
        ?? throw new InvalidOperationException("Telegram:BotToken is not configured");
    return new TelegramBotClient(token);
});

builder.Services.AddSingleton(sp =>
{
    return StorageClient.Create();
});

builder.Services.AddHttpClient<IExtractionService, ExtractionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddSingleton<IGcsService, GcsService>();
builder.Services.AddSingleton<ICustomerLeadService, CustomerLeadService>();

var app = builder.Build();

app.MapGet("/", () => "Nexus Intake API — Operational");

app.MapPost("/webhook/telegram", async (
    HttpContext context,
    ITelegramService telegram,
    IGcsService gcs,
    IExtractionService extraction,
    ICustomerLeadService leads) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        var update = System.Text.Json.JsonSerializer.Deserialize<Telegram.Bot.Types.Update>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (update?.Message is not { } message)
            return Results.Ok();

        var chatId = message.Chat.Id;

        if (message.Photo is { Length: > 0 } photos)
        {
            await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.ProcessingStart());

            var largestPhoto = photos.MaxBy(p => p.FileSize ?? p.Width * p.Height);
            if (largestPhoto is null)
            {
                await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.Error("No photo data found"));
                return Results.Ok();
            }

            var photoData = await telegram.DownloadPhotoAsync(largestPhoto.FileId);

            var objectName = $"intakes/{message.MessageId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.jpg";
            var gcsUri = await gcs.UploadAsync(objectName, photoData, "image/jpeg");

            await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.SystemLog("DOCUMENT ARCHIVED TO NEXUS CLOUD"));

            var extractionResult = await extraction.ExtractAsync(gcsUri);

            if (!string.IsNullOrEmpty(extractionResult.Error))
            {
                await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.Error(extractionResult.Error));
                return Results.Ok();
            }

            var (lead, errors) = leads.ProcessExtraction(extractionResult, gcsUri);

            if (errors.Count > 0)
            {
                if (errors.Any(e => e.Contains("blurry") || e.Contains("confidence")))
                {
                    await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.ErrorBlurryImage());
                }
                else
                {
                    await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.FormatValidationErrors(errors));
                }
                return Results.Ok();
            }

            await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.ProcessingComplete());

            var resultMessage = lead.DocumentType switch
            {
                DocumentType.Kimlik => CyberTerminalFormatter.FormatKimlikResult(lead),
                DocumentType.InsurancePolicy => CyberTerminalFormatter.FormatPolicyResult(lead),
                _ => CyberTerminalFormatter.SystemLog("DOCUMENT PROCESSED")
            };

            await telegram.SendMessageAsync(chatId, resultMessage);
        }
        else if (message.Text is { } text)
        {
            var response = text.ToLowerInvariant() switch
            {
                "/start" => "*`<< NEXUS INTAKE v1.0 ONLINE >>`*\n\nSend a photo of a TRNC Kimlik or Insurance Policy to begin extraction\\.",
                "/help" => "`[HELP]` Send a photo of:\n\\- TRNC Kimlik \\(ID Card\\)\n\\- Insurance Policy\n\nThe system will extract data automatically\\.",
                _ => CyberTerminalFormatter.SystemLog("AWAITING DOCUMENT SCAN. SEND PHOTO TO PROCEED.")
            };
            await telegram.SendMessageAsync(chatId, response);
        }

        return Results.Ok();
    }
    catch (Exception)
    {
        return Results.Ok();
    }
});

app.Run();
