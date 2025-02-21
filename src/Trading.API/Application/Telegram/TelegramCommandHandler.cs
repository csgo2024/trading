using Telegram.Bot.Types;
using Trading.API.Application.Telegram.Handlers;

namespace Trading.API.Application.Telegram
{
    public class TelegramCommandHandler : ITelegramCommandHandler
    {
        private readonly ILogger<TelegramCommandHandler> _logger;
        private readonly CommandHandlerFactory _handlerFactory;

        public TelegramCommandHandler(
            ILogger<TelegramCommandHandler> logger,
            CommandHandlerFactory handlerFactory)
        {
            _logger = logger;
            _handlerFactory = handlerFactory;
        }

        public async Task HandleCommand(Message message)
        {
            if (message?.Text == null) return;

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
}