namespace Trading.API.Application.Telegram.Handlers;

public interface ICommandHandler
{
    Task HandleAsync(string parameters);
}
