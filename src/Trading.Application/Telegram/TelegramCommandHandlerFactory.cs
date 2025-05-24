using Trading.Application.Telegram.Handlers;

namespace Trading.Application.Telegram;

public class TelegramCommandHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _handlers;

    public TelegramCommandHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _handlers = new Dictionary<string, Type>
        {
            {AlertCommandHandler.CallbackPrefix, typeof(AlertCommandHandler)},
            {AlertCommandHandler.Command, typeof(AlertCommandHandler)},
            {HelpCommandHandler.Command, typeof(HelpCommandHandler)},
            {StrategyCommandHandler.CallbackPrefix, typeof(StrategyCommandHandler)},
            {StrategyCommandHandler.Command, typeof(StrategyCommandHandler)},
        };
    }

    public virtual ICommandHandler? GetHandler(string command)
    {
        return _handlers.TryGetValue(command, out var handlerType)
            ? _serviceProvider.GetService(handlerType) as ICommandHandler
            : null;
    }
}
