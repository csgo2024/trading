namespace Trading.Application.Telegram.Handlers;

public interface ICommandHandler
{
    Task HandleAsync(string parameters);

    Task HandleCallbackAsync(string action, string parameters);

}
