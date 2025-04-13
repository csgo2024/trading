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
            {HelpCommandHandler.Command, typeof(HelpCommandHandler)},
            {StatusCommandHandler.Command, typeof(StatusCommandHandler)},
            {CreateStrategyHandler.Command, typeof(CreateStrategyHandler)},
            {DeleteStrategyHandler.Command, typeof(DeleteStrategyHandler)},
            {StopStrategyHandler.Command, typeof(StopStrategyHandler)},
            {ResumeStrategyHandler.Command, typeof(ResumeStrategyHandler)},
            {PriceAlertCommandHandler.Command, typeof(PriceAlertCommandHandler)},
        };
    }

    public virtual ICommandHandler? GetHandler(string command)
    {
        return _handlers.TryGetValue(command, out var handlerType)
            ? _serviceProvider.GetService(handlerType) as ICommandHandler
            : null;
    }
}
