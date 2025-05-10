using Microsoft.Extensions.Logging;
using Trading.Application.JavaScript;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class CloseSellExecutor : BaseExecutor
{
    private readonly IAccountProcessorFactory _accountProcessorFactory;
    public CloseSellExecutor(ILogger<CloseSellExecutor> logger,
                            IAccountProcessorFactory accountProcessorFactory,
                            IStrategyRepository strategyRepository,
                            JavaScriptEvaluator javaScriptEvaluator)
        : base(logger, strategyRepository, javaScriptEvaluator)
    {
        _accountProcessorFactory = accountProcessorFactory;
    }

    public override async Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        var strategies = GetMonitoringStrategy(StrategyType.CloseSell).Where(x => x.Symbol == notification.Symbol
                                && x.Interval == BinanceHelper.ConvertToIntervalString(notification.Interval));
        var tasks = strategies.Select(async strategy =>
        {
            if (strategy.AccountType == AccountType.Spot)
            {
                return;
            }
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            if (accountProcessor != null)
            {
                if (strategy.OrderId is null)
                {
                    var filterData = await accountProcessor.GetSymbolFilterData(strategy, cancellationToken);
                    var closePrice = notification.Kline.ClosePrice;
                    strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(closePrice * (1 + strategy.Volatility), filterData.Item1);
                    strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
                    await TryPlaceOrder(accountProcessor, strategy, cancellationToken);
                }
                if (ShouldStopLoss(accountProcessor, strategy, notification))
                {
                    await TryStopOrderAsync(accountProcessor, strategy, notification.Kline.ClosePrice, cancellationToken);
                }
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
            }
        });
        await Task.WhenAll(tasks);
    }
}
