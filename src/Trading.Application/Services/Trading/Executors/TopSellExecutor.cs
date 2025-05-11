using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Trading.Application.JavaScript;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class TopSellExecutor : BaseExecutor
{
    public TopSellExecutor(ILogger<TopSellExecutor> logger,
                           IStrategyRepository strategyRepository,
                           JavaScriptEvaluator javaScriptEvaluator)
        : base(logger, strategyRepository, javaScriptEvaluator)
    {
    }

    public override bool ShouldStopLoss(IAccountProcessor accountProcessor, Strategy strategy, KlineClosedEvent @event)
    {
        return false;
    }
    public override async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var currentDate = DateTime.UtcNow.Date;
        if (strategy.HasOpenOrder && strategy.OrderPlacedTime.HasValue && strategy.OrderPlacedTime.Value.Date != currentDate)
        {
            _logger.LogInformation("[{AccountType}-{Symbol}] Previous day's order not filled, cancelling order before reset.",
                                   strategy.AccountType,
                                   strategy.Symbol);
            await CancelExistingOrder(accountProcessor, strategy, ct);
        }
        if (strategy.OrderId is null)
        {
            await ResetDailyStrategy(accountProcessor, strategy, currentDate, ct);
            await TryPlaceOrder(accountProcessor, strategy, ct);
        }
        await base.ExecuteAsync(accountProcessor, strategy, ct);
    }

    public async Task ResetDailyStrategy(IAccountProcessor accountProcessor, Strategy strategy, DateTime currentDate, CancellationToken ct)
    {
        var kLines = await accountProcessor.GetKlines(strategy.Symbol, KlineInterval.OneDay, startTime: currentDate, limit: 1, ct: ct);
        if (kLines.Success && kLines.Data.Any())
        {
            var openPrice = CommonHelper.TrimEndZero(kLines.Data.First().OpenPrice);
            var filterData = await accountProcessor.GetSymbolFilterData(strategy, ct);
            strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(openPrice * (1 + strategy.Volatility), filterData.Item1);
            strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
            strategy.HasOpenOrder = false;
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
        }
        else
        {
            _logger.LogErrorWithAlert("[{AccountType}-{Symbol}] Failed to get daily open price. Error: {ErrorMessage}.",
                                      strategy.AccountType,
                                      strategy.Symbol,
                                      kLines.Error?.Message);
        }
    }

    public override Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
