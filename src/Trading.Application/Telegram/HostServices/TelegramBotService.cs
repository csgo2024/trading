using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace Trading.Application.Telegram.HostServices;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramCommandHandler _commandHandler;

    public TelegramBotService(ITelegramBotClient botClient,
                              ITelegramCommandHandler commandHandler,
                              ILogger<TelegramBotService> logger)
    {
        _botClient = botClient;
        _commandHandler = commandHandler;
        _logger = logger;
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
            _logger.LogError(ex, "<pre>Failed to start bot service</pre>");
        }

        await Task.Delay(-1, cancellationToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.CallbackQuery is { } callbackQuery)
            {
                await _commandHandler.HandleCallbackQuery(callbackQuery);
            }

            if (update.Message is { } message && message.Text is { } messageText)
            {
                if (messageText.StartsWith('/'))
                {
                    await _commandHandler.HandleCommand(message);
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "<pre>Error handling update {UpdateId}</pre>", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // _logger.LogError(exception, "<pre>Telegram Polling Error</pre>");
        return Task.CompletedTask;
    }
}
