using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class BottomBuyExecutor : BaseExecutor
{
    private readonly IStrategyRepository _strategyRepository;

    public BottomBuyExecutor(ILogger<BottomBuyExecutor> logger,
                             IStrategyRepository strategyRepository) : base(logger)
    {
        _strategyRepository = strategyRepository;
    }

    public override async Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var currentDate = DateTime.UtcNow.Date;
        if (strategy.LastTradeDate?.Date != currentDate)
        {
            if (strategy.HasOpenOrder && strategy.OrderPlacedTime.HasValue)
            {
                _logger.LogInformation("[{AccountType}-{Symbol}] Previous day's order not filled, cancelling order before reset.",
                    strategy.AccountType, strategy.Symbol);
                await CancelExistingOrder(accountProcessor, strategy, ct);
            }
            await ResetDailyStrategy(accountProcessor, strategy, currentDate, ct);
        }

        if (!strategy.IsTradedToday)
        {
            if (strategy.HasOpenOrder)
            {
                await CheckOrderStatus(accountProcessor, strategy, ct);
            }
            else
            {
                await TryPlaceOrder(accountProcessor, strategy, ct);
            }
        }

        strategy.UpdatedAt = DateTime.Now;
        var success = await _strategyRepository.UpdateAsync(strategy.Id, strategy, ct);
        if (!success)
        {
            throw new InvalidOperationException("Failed to update strategy order.");
        }
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
            strategy.LastTradeDate = currentDate;
            strategy.IsTradedToday = false;
            strategy.HasOpenOrder = false;
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
            _logger.LogInformation("[{AccountType}-{Symbol}] New day started, Open price: {OpenPrice}, Target price: {TargetPrice}.",
                strategy.AccountType, strategy.Symbol, openPrice, strategy.TargetPrice);
        }
        else
        {
            _logger.LogErrorWithAlert("[{AccountType}-{Symbol}] Failed to get daily open price. Error: {ErrorMessage}.",
                strategy.AccountType, strategy.Symbol, kLines.Error?.Message);
        }
    }
}
