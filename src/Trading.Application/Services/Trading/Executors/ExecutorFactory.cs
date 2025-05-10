using Trading.Common.Enums;

namespace Trading.Application.Services.Trading.Executors;

public interface IExecutorFactory
{
    BaseExecutor? GetExecutor(StrategyType strategyType);
}

public class ExecutorFactory : IExecutorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<StrategyType, Type> _handlers;

    public ExecutorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _handlers = new Dictionary<StrategyType, Type>
        {
            {StrategyType.TopSell, typeof(TopSellExecutor)},
            {StrategyType.BottomBuy, typeof(BottomBuyExecutor)},
            {StrategyType.CloseBuy, typeof(CloseBuyExecutor)},
            {StrategyType.CloseSell, typeof(CloseSellExecutor)},
            {StrategyType.DCA, typeof(DCABuyExecutor)},
        };
    }

    public BaseExecutor? GetExecutor(StrategyType strategyType)
    {
        return _handlers.TryGetValue(strategyType, out var handlerType)
            ? _serviceProvider.GetService(handlerType) as BaseExecutor
            : null;
    }
}
