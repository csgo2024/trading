using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Common.Helpers;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class BottomBuyExecutor : BaseExecutor
{
    public override StrategyType StrategyType => StrategyType.BottomBuy;

    public BottomBuyExecutor(ILogger<BottomBuyExecutor> logger,
                             IStrategyRepository strategyRepository,
                             JavaScriptEvaluator javaScriptEvaluator,
                             IAccountProcessorFactory accountProcessorFactory,
                             IStrategyStateManager strategyStateManager)
        : base(logger, strategyRepository, javaScriptEvaluator, accountProcessorFactory, strategyStateManager)
    {
    }
    public override async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var currentDate = DateTime.UtcNow.Date;
        if (strategy.OrderPlacedTime.HasValue && strategy.OrderPlacedTime.Value.Date != currentDate)
        {
            if (strategy.HasOpenOrder)
            {
                _logger.LogInformation("[{AccountType}-{Symbol}] Previous day's order not filled, cancelling order before reset.",
                                       strategy.AccountType,
                                       strategy.Symbol);
                await CancelExistingOrder(accountProcessor, strategy, ct);
            }
            strategy.RequireReset = true;
        }
        if (strategy.RequireReset)
        {
            await ResetDailyStrategy(accountProcessor, strategy, currentDate, ct);
        }
        if (strategy.OrderId is null)
        {
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
            strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(openPrice * (1 - strategy.Volatility), filterData.Item1);
            strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
            strategy.OpenPrice = openPrice;
            strategy.HasOpenOrder = false;
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
            strategy.RequireReset = false;
            _logger.LogInformation("[{AccountType}-{Symbol}] New day started, Open price: {OpenPrice}, Target price: {TargetPrice}.",
                strategy.AccountType, strategy.Symbol, openPrice, strategy.TargetPrice);
            return;
        }
        _logger.LogErrorWithAlert("[{AccountType}-{Symbol}] Failed to get daily open price. Error: {ErrorMessage}.",
                                  strategy.AccountType,
                                  strategy.Symbol,
                                  kLines.Error?.Message);
    }
}
