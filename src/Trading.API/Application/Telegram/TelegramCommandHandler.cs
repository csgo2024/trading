using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace Trading.API.Application.Telegram;

public class TelegramCommandHandler : ITelegramCommandHandler
{
    private readonly ILogger<TelegramCommandHandler> _logger;
    private readonly TelegramCommandHandlerFactory _handlerFactory;

    public TelegramCommandHandler(
        ILogger<TelegramCommandHandler> logger,
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

    private static (string command, string parameters) ParseCommand(string messageText)
    {
        var index = messageText.IndexOf(' ');
        return index == -1
            ? (messageText, string.Empty)
            : (messageText[..index], messageText[(index + 1)..]);
    }
}
