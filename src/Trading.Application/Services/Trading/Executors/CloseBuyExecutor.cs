using Microsoft.Extensions.Logging;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class CloseBuyExecutor : BaseExecutor
{
    public CloseBuyExecutor(ILogger<CloseBuyExecutor> logger,
        IAccountProcessorFactory accountProcessorFactory,
        IStrategyRepository strategyRepository,
        JavaScriptEvaluator javaScriptEvaluator,
        IStrategyStateManager strategyStateManager)
        : base(logger, strategyRepository, javaScriptEvaluator, accountProcessorFactory, strategyStateManager)
    {
    }

    public override StrategyType StrategyType => StrategyType.CloseBuy;

    public override async Task HandleKlineClosedEvent(IAccountProcessor accountProcessor, Strategy strategy, KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        if (strategy.OrderId is null)
        {
            var filterData = await accountProcessor.GetSymbolFilterData(strategy, cancellationToken);
            var closePrice = notification.Kline.ClosePrice;
            strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(closePrice * (1 - strategy.Volatility), filterData.Item1);
            strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
            await TryPlaceOrder(accountProcessor, strategy, cancellationToken);
        }
    }
}
