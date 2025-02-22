using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Trading.API.Application.Telegram;

namespace Trading.API.HostServices;

public class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramCommandHandler _commandHandler;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        ITelegramBotClient botClient,
        ITelegramCommandHandler commandHandler,
        ILogger<TelegramBotService> logger
    )
    {
        _botClient = botClient;
        _commandHandler = commandHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: new ReceiverOptions { AllowedUpdates = { } },
                cancellationToken: stoppingToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "<pre>Failed to start bot service</pre>");
        }

        await Task.Delay(-1, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message is not { } message)
                return;

            if (message.Text is not { } messageText)
                return;

            if (messageText.StartsWith('/'))
            {
                await _commandHandler.HandleCommand(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "<pre>Error handling update {UpdateId}</pre>", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "<pre>Telegram Polling Error</pre>");
        return Task.CompletedTask;
    }
}