using Telegram.Bot.Types;

namespace Trading.API.Application.Telegram
{
    public interface ITelegramCommandHandler
    {
        Task HandleCommand(Message message);
    }
}
