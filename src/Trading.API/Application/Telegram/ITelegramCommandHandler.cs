using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace Trading.API.Application.Telegram;

public interface ITelegramCommandHandler
{
    Task HandleCommand([NotNull] Message message);
}
