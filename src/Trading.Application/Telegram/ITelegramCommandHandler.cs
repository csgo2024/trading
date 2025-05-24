using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace Trading.Application.Telegram;

public interface ITelegramCommandHandler
{
    Task HandleCommand([NotNull] Message message);
    Task HandleCallbackQuery(CallbackQuery? callbackQuery);
}
