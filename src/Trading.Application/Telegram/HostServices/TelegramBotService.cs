using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Trading.Application.Telegram.Handlers;

namespace Trading.Application.Telegram.HostServices;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramCommandHandler _commandHandler;

    public TelegramBotService(
        ITelegramBotClient botClient,
        ITelegramCommandHandler commandHandler,
        ILogger<TelegramBotService> logger,
        IServiceProvider serviceProvider
    )
    {
        _botClient = botClient;
        _commandHandler = commandHandler;
        _logger = logger;
        _serviceProvider = serviceProvider;
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
                if (callbackQuery.Data?.StartsWith("pause_") == true ||
                    callbackQuery.Data?.StartsWith("resume_") == true)
                {
                    var handler = _serviceProvider.GetService<AlarmCommandHandler>();
                    if (handler != null)
                    {
                        await handler.HandleCallbackAsync(callbackQuery.Data);
                    }
                }
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
