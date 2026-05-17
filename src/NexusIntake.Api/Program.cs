using Telegram.Bot;
using Google.Cloud.Storage.V1;
using NexusIntake.Api.Services;
using NexusIntake.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

// Cloud Run dynamically assigns a port — fall back to 8080 for local dev
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var token = Environment.GetEnvironmentVariable("Telegram__BotToken")
                ?? sp.GetRequiredService<IConfiguration>()["Telegram:BotToken"];

    if (string.IsNullOrWhiteSpace(token))
        throw new InvalidOperationException(
            "Bot token not found. Set Telegram__BotToken env var or use 'dotnet user-secrets set Telegram:BotToken <token>'.");

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
    ICustomerLeadService leads,
    IConfiguration config,
    ILogger<Program> logger) =>
{
    // Validate Telegram secret token to prevent unauthorized spoofing
    var expectedSecret = config["Telegram:WebhookSecret"];
    if (!string.IsNullOrWhiteSpace(expectedSecret))
    {
        if (!context.Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var receivedSecret) ||
            receivedSecret != expectedSecret)
        {
            logger.LogWarning("Unauthorized webhook attempt blocked.");
            return Results.Unauthorized();
        }
    }

    logger.LogInformation("[🚨 SYSTEM ALERT] WEBHOOK ENDPOINT WAS JUST HIT!");
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        logger.LogInformation("[LOG] Webhook hit. Deserializing payload...");
        var update = Newtonsoft.Json.JsonConvert.DeserializeObject<Telegram.Bot.Types.Update>(body);

        if (update?.Message == null)
        {
            logger.LogWarning("[WARNING] Deserialization failed: Message object is null.");
            return Results.Ok();
        }

        // Guard against non-photo/file uploads (compressed photos vs raw files)
        if (update.Message.Photo == null && update.Message.Document == null)
        {
            var chatId = update.Message.Chat.Id;
            await telegram.SendMessageAsync(chatId, CyberTerminalFormatter.Error("Please send the ID as a compressed Photo, not a raw file."));
            return Results.Ok();
        }

        logger.LogInformation("[LOG] Message captured! Photo array length: {PhotoCount}", update.Message.Photo?.Length ?? 0);

        var message = update.Message;
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
            await gcs.DeleteObjectAsync(objectName); // SHRED THE PHOTO

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
    catch ( Exception ex)
    {
        Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        return Results.Ok();
    }
});

app.Run();
