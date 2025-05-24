using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Trading.Common.Models;

namespace Trading.Application.Telegram.HostServices;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramCommandHandler _commandHandler;
    private readonly TelegramSettings _telegramSettings;

    public TelegramBotService(ITelegramBotClient botClient,
                              ITelegramCommandHandler commandHandler,
                              IOptions<TelegramSettings> telegramSettingOptions,
                              ILogger<TelegramBotService> logger)
    {
        _botClient = botClient;
        _commandHandler = commandHandler;
        _logger = logger;
        _telegramSettings = telegramSettingOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: new ReceiverOptions(),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start bot service");
        }

        await Task.Delay(-1, cancellationToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.CallbackQuery is { } callbackQuery)
            {
                if (callbackQuery.From.Id != _telegramSettings.UserId)
                {
                    _logger.LogError("Permission denied");
                    return;
                }
                await _commandHandler.HandleCallbackQuery(callbackQuery);
            }

            if (update.Message is { } message && message.Text is { } messageText)
            {
                if (update.Message.From?.Id != _telegramSettings.UserId)
                {
                    _logger.LogError("Permission denied");
                    return;
                }
                if (messageText.StartsWith('/'))
                {
                    await _commandHandler.HandleCommand(message);
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // _logger.LogError(exception, "Telegram Polling Error");
        return Task.CompletedTask;
    }
}
