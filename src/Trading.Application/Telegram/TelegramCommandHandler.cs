using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;

namespace Trading.Application.Telegram;

public class TelegramCommandHandler : ITelegramCommandHandler
{
    private readonly ILogger<TelegramCommandHandler> _logger;
    private readonly TelegramCommandHandlerFactory _handlerFactory;

    public TelegramCommandHandler(ILogger<TelegramCommandHandler> logger,
                                  TelegramCommandHandlerFactory handlerFactory)
    {
        _logger = logger;
        _handlerFactory = handlerFactory;
    }

    public async Task HandleCommand([NotNull] Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            return;
        }

        var (command, parameters) = ParseCommand(message.Text);

        try
        {
            var handler = _handlerFactory.GetHandler(command);
            if (handler != null)
            {
                await handler.HandleAsync(parameters);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed");
        }
    }

    public async Task HandleCallbackQuery(CallbackQuery? callbackQuery)
    {
        var (prefix, action, parameters) = ParseCallbackQuery(callbackQuery);

        if (string.IsNullOrEmpty(prefix))
        {
            return;
        }

        try
        {
            var handler = _handlerFactory.GetHandler(prefix);
            if (handler != null)
            {
                await handler.HandleCallbackAsync(action, parameters);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback execution failed");
        }
    }

    private static (string command, string parameters) ParseCommand(string messageText)
    {
        var index = messageText.IndexOf(' ');
        return index == -1
            ? (messageText, string.Empty)
            : (messageText[..index], messageText[(index + 1)..]);
    }
    private static (string prefix, string action, string parameters) ParseCallbackQuery(CallbackQuery? callbackQuery)
    {
        if (callbackQuery == null || string.IsNullOrEmpty(callbackQuery.Data))
        {
            return (string.Empty, string.Empty, string.Empty);
        }
        var data = callbackQuery.Data;
        var parts = data.Trim().Split(['_'], 3);
        if (parts.Length < 3)
        {
            return (string.Empty, string.Empty, string.Empty);
        }
        return (parts[0], parts[1], parts[2]);
    }

}
