namespace Trading.API.Application.Telegram.Handlers
{
    public class CommandHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _handlers;

        public CommandHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _handlers = new Dictionary<string, Type>
            {
                {StartCommandHandler.Command, typeof(StartCommandHandler)},
                {StatusCommandHandler.Command, typeof(StatusCommandHandler)},
                {CreateStrategyHandler.Command, typeof(CreateStrategyHandler)},
                {DeleteStrategyHandler.Command, typeof(DeleteStrategyHandler)},
                {StopStrategyHandler.Command, typeof(StopStrategyHandler)},
                {ResumeStrategyHandler.Command, typeof(ResumeStrategyHandler)},
            };
        }

        public ICommandHandler? GetHandler(string command)
        {
            return _handlers.TryGetValue(command, out var handlerType)
                ? _serviceProvider.GetService(handlerType) as ICommandHandler
                : null;
        }
    }
}