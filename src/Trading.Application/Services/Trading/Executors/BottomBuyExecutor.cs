using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Trading.Application.Helpers;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading.Executors;

public class BottomBuyExecutor : IExecutor
{
    private readonly ILogger<BottomBuyExecutor> _logger;
    private readonly IStrategyRepository _strategyRepository;

    public BottomBuyExecutor(
        ILogger<BottomBuyExecutor> logger,
        IStrategyRepository strategyRepository)
    {
        _logger = logger;
        _strategyRepository = strategyRepository;
    }

    public async Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{AccountType}-{Symbol}] Error processing.",
                strategy.AccountType, strategy.Symbol);
            throw;
        }
    }

    public async Task CheckOrderStatus(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        if (strategy.OrderId is null)
        {
            strategy.HasOpenOrder = false;
            return;
        }

        var orderStatus = await accountProcessor.GetOrder(strategy.Symbol, strategy.OrderId.Value, ct);
        if (orderStatus.Success)
        {
            switch (orderStatus.Data.Status)
            {
                case OrderStatus.Filled:
                    _logger.LogInformation("[{AccountType}-{Symbol}] Order filled successfully at price: {Price}.",
                        strategy.AccountType, strategy.Symbol, strategy.TargetPrice);
                    strategy.IsTradedToday = true;
                    strategy.HasOpenOrder = false;
                    strategy.OrderId = null;
                    strategy.OrderPlacedTime = null;
                    break;

                case OrderStatus.Canceled:
                case OrderStatus.Expired:
                case OrderStatus.Rejected:
                    _logger.LogInformation("[{AccountType}-{Symbol}] Order {Status}. Will try to place new order.",
                        strategy.AccountType, strategy.Symbol, orderStatus.Data.Status);
                    strategy.HasOpenOrder = false;
                    strategy.OrderId = null;
                    strategy.OrderPlacedTime = null;
                    break;

                default:
                    if (strategy.OrderPlacedTime.HasValue && strategy.OrderPlacedTime.Value.Date != DateTime.UtcNow.Date)
                    {
                        _logger.LogInformation("[{AccountType}-{Symbol}] Order from previous day detected, initiating cancellation.",
                            strategy.AccountType, strategy.Symbol);
                        await CancelExistingOrder(accountProcessor, strategy, ct);
                    }
                    break;
            }
        }
        else
        {
            _logger.LogError("[{AccountType}-{Symbol}] Failed to check order status, Error: {ErrorMessage}.",
                strategy.AccountType, strategy.Symbol, orderStatus.Error?.Message);
        }
    }

    public async Task CancelExistingOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        if (strategy.OrderId is null)
        {
            return;
        }

        try
        {
            var cancelResult = await accountProcessor.CancelOrder(strategy.Symbol, strategy.OrderId.Value, ct);
            if (cancelResult.Success)
            {
                _logger.LogInformation("[{AccountType}-{Symbol}] Successfully cancelled order, OrderId: {OrderId}",
                    strategy.AccountType, strategy.Symbol, strategy.OrderId);
                strategy.HasOpenOrder = false;
                strategy.OrderId = null;
                strategy.OrderPlacedTime = null;
            }
            else
            {
                _logger.LogError("[{AccountType}-{Symbol}] Failed to cancel order. Error: {ErrorMessage}",
                    strategy.AccountType, strategy.Symbol, cancelResult.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{AccountType}-{Symbol}] Error while cancelling order {OrderId}.",
                strategy.AccountType, strategy.Symbol, strategy.OrderId);
        }
    }

    public async Task ResetDailyStrategy(IAccountProcessor accountProcessor, Strategy strategy, DateTime currentDate, CancellationToken ct)
    {
        var kLines = await accountProcessor.GetKlines(strategy.Symbol, KlineInterval.OneDay, startTime: currentDate, limit: 1, ct: ct);
        if (kLines.Success && kLines.Data.Any())
        {
            var openPrice = CommonHelper.TrimEndZero(kLines.Data.First().OpenPrice);
            var filterData = await accountProcessor.GetSymbolFilterData(strategy, ct);
            strategy.TargetPrice = CommonHelper.AdjustPriceByStepSize(openPrice * (1 - strategy.PriceDropPercentage), filterData.Item1);
            strategy.Quantity = CommonHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
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
            _logger.LogError("[{AccountType}-{Symbol}] Failed to get daily open price. Error: {ErrorMessage}.",
                strategy.AccountType, strategy.Symbol, kLines.Error?.Message);
        }
    }

    public async Task TryPlaceOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var quantity = CommonHelper.TrimEndZero(strategy.Quantity);
        var price = CommonHelper.TrimEndZero(strategy.TargetPrice);
        var orderResult = await accountProcessor.PlaceOrder(
            strategy.Symbol,
            quantity,
            price,
            TimeInForce.GoodTillCanceled,
            ct);

        if (orderResult.Success)
        {
            _logger.LogInformation("[{AccountType}-{Symbol}] Order placed successfully. Quantity: {Quantity}, Price: {Price}.",
                strategy.AccountType, strategy.Symbol, quantity, price);
            strategy.OrderId = orderResult.Data.Id;
            strategy.HasOpenOrder = true;
            strategy.OrderPlacedTime = DateTime.UtcNow;
        }
        else
        {
            _logger.LogError("[{AccountType}-{Symbol}] Failed to place order. Error: {ErrorMessage}, TargetPrice:{Price}, Quantity: {Quantity}.",
                strategy.AccountType, strategy.Symbol, orderResult.Error?.Message, price, quantity);
        }
    }
}
