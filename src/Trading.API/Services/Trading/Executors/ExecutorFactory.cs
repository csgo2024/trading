using Trading.Domain.Entities;

namespace Trading.API.Services.Trading.Executors;

public class ExecutorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<StrategyType, Type> _handlers;

    public ExecutorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _handlers = new Dictionary<StrategyType, Type>
        {
            {StrategyType.BuyBottom, typeof(BottomBuyExecutor)},
            {StrategyType.DCA, typeof(DCABuyExecutor)},
        };
    }

    public virtual IExecutor? GetExecutor(StrategyType strategyType)
    {
        return _handlers.TryGetValue(strategyType, out var handlerType)
            ? _serviceProvider.GetService(handlerType) as IExecutor
            : null;
    }
}
