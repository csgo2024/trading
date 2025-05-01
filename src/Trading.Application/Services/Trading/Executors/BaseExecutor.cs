using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Common.Helpers;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading.Executors;

public abstract class BaseExecutor
{
    protected readonly ILogger _logger;
    public BaseExecutor(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Task Execute(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct);
    private static Task<WebCallResult<BinanceOrderBase>> PlaceOrderAsync(IAccountProcessor accountProcessor,
                                                                         Strategy strategy,
                                                                         CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.TopSell ||
            strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.PlaceShortOrderAsync(
                strategy.Symbol,
                strategy.Quantity,
                strategy.TargetPrice,
                TimeInForce.GoodTillCanceled,
                ct);
        }
        else if (strategy.StrategyType == StrategyType.BottomBuy ||
                 strategy.StrategyType == StrategyType.CloseBuy)
        {
            return accountProcessor.PlaceLongOrderAsync(
                strategy.Symbol,
                strategy.Quantity,
                strategy.TargetPrice,
                TimeInForce.GoodTillCanceled,
                ct);
        }
        else
        {
            throw new NotSupportedException($"Strategy type {strategy.StrategyType} is not supported.");
        }
    }

    public virtual async Task CancelExistingOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        if (strategy.OrderId is null)
        {
            return;
        }

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

    public virtual async Task CheckOrderStatus(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
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
            _logger.LogInformation("[{AccountType}-{Symbol}] Failed to check order status, Error: {ErrorMessage}.",
                strategy.AccountType, strategy.Symbol, orderStatus.Error?.Message);
        }
    }
    public virtual async Task TryPlaceOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var quantity = CommonHelper.TrimEndZero(strategy.Quantity);
        var price = CommonHelper.TrimEndZero(strategy.TargetPrice);
        strategy.TargetPrice = price;
        strategy.Quantity = quantity;
        var maxRetries = 3;
        var currentRetry = 0;
        var errorMessage = string.Empty;

        while (currentRetry < maxRetries)
        {
            try
            {
                var orderResult = await PlaceOrderAsync(accountProcessor, strategy, ct);

                if (orderResult.Success)
                {
                    _logger.LogInformationWithAlert("[{StrategyType}-{AccountType}-{Symbol}] Order placed successfully. Quantity: {Quantity}, Price: {Price}.",
                                           strategy.StrategyType,
                                           strategy.AccountType,
                                           strategy.Symbol,
                                           quantity,
                                           price);
                    strategy.OrderId = orderResult.Data.Id;
                    strategy.HasOpenOrder = true;
                    strategy.OrderPlacedTime = DateTime.UtcNow;
                    return;
                }
                errorMessage = orderResult.Error?.Message ?? "Unknown error";

                currentRetry++;
                if (currentRetry < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, currentRetry));
                    _logger.LogWarning("[{StrategyType}-{AccountType}-{Symbol}] Attempt {RetryCount} of {MaxRetries} failed. Retrying in {Delay} seconds. Error: {Error}",
                                       strategy.StrategyType,
                                       strategy.AccountType,
                                       strategy.Symbol,
                                       currentRetry,
                                       maxRetries,
                                       delay.TotalSeconds,
                                       orderResult.Error?.Message);
                    await Task.Delay(delay, ct);
                }
            }
            catch (Exception ex)
            {
                currentRetry++;
                if (currentRetry < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, currentRetry));
                    _logger.LogWarning("[{StrategyType}-{AccountType}-{Symbol}] Attempt {RetryCount} of {MaxRetries} failed with exception. Retrying in {Delay} seconds. Error: {Error}",
                                       strategy.StrategyType,
                                       strategy.AccountType,
                                       strategy.Symbol,
                                       currentRetry,
                                       maxRetries,
                                       delay.TotalSeconds,
                                       ex.Message);
                    await Task.Delay(delay, ct);
                }
            }
        }

        _logger.LogErrorWithAlert("""
        [{StrategyType}-{AccountType}-{Symbol}] Failed to place order after {MaxRetries} attempts.
        StrategyId: {StrategyId}
        Error: {ErrorMessage}
        TargetPrice:{Price}, Quantity: {Quantity}.
        """, strategy.StrategyType, strategy.AccountType, strategy.Symbol, maxRetries, strategy.Id,
            errorMessage, price, quantity);
    }

}
