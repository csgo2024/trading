using Microsoft.Extensions.Logging;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class CloseSellExecutor : BaseExecutor
{
    public CloseSellExecutor(ILogger<CloseSellExecutor> logger,
        IAccountProcessorFactory accountProcessorFactory,
        IStrategyRepository strategyRepository,
        JavaScriptEvaluator javaScriptEvaluator,
        IStrategyStateManager stateManager)
        : base(logger, strategyRepository, javaScriptEvaluator, accountProcessorFactory, stateManager)
    {
    }

    public override StrategyType StrategyType => StrategyType.CloseSell;

    public override async Task HandleKlineClosedEvent(IAccountProcessor accountProcessor, Strategy strategy, KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        if (strategy.AccountType == AccountType.Spot)
        {
            return;
        }
        if (strategy.OrderId is null)
        {
            var filterData = await accountProcessor.GetSymbolFilterData(strategy, cancellationToken);
            var closePrice = notification.Kline.ClosePrice;
            strategy.OpenPrice = closePrice; // Update open price to the current close price
            strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(closePrice * (1 + strategy.Volatility), filterData.Item1);
            strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
            await TryPlaceOrder(accountProcessor, strategy, cancellationToken);
        }
    }

    public override async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        if (strategy.AccountType == AccountType.Spot)
        {
            return;
        }
        if (strategy.OpenPrice is null || strategy.TargetPrice <= 0 || strategy.Quantity <= 0)
        {
            return;
        }
        if (strategy.OrderId is null)
        {
            await TryPlaceOrder(accountProcessor, strategy, ct);
        }
        await base.ExecuteAsync(accountProcessor, strategy, ct);
    }
}
