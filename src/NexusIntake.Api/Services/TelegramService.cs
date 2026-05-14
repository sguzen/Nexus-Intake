using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NexusIntake.Api.Services;

public interface ITelegramService
{
    Task<byte[]> DownloadPhotoAsync(string fileId, CancellationToken ct = default);
    Task SendMessageAsync(long chatId, string text, CancellationToken ct = default);
}

public class TelegramService : ITelegramService
{
    private readonly ITelegramBotClient _botClient;

    public TelegramService(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task<byte[]> DownloadPhotoAsync(string fileId, CancellationToken ct = default)
    {
        var file = await _botClient.MakeRequestAsync(new GetFileRequest(fileId), ct);
        using var ms = new MemoryStream();
        await _botClient.DownloadFileAsync(file.FilePath!, ms, ct);
        return ms.ToArray();
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken ct = default)
    {
        var request = new SendMessageRequest(new ChatId(chatId), text)
        {
            ParseMode = ParseMode.MarkdownV2
        };
        await _botClient.MakeRequestAsync(request, ct);
    }
}
