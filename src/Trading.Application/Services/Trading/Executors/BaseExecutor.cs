using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using CryptoExchange.Net.Objects;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Common.Helpers;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading.Executors;

public abstract class BaseExecutor :
    INotificationHandler<KlineClosedEvent>
{
    protected readonly ILogger _logger;
    protected readonly IStrategyRepository _strategyRepository;
    protected readonly JavaScriptEvaluator _javaScriptEvaluator;
    protected readonly IStrategyStateManager _stateManager;

    public BaseExecutor(ILogger logger,
                        IStrategyRepository strategyRepository,
                        JavaScriptEvaluator javaScriptEvaluator,
                        IStrategyStateManager strategyStateManager)
    {
        _strategyRepository = strategyRepository;
        _javaScriptEvaluator = javaScriptEvaluator;
        _logger = logger;
        _stateManager = strategyStateManager;
    }

    public abstract StrategyType StrategyType { get; }
    private static Task<WebCallResult<BinanceOrderBase>> PlaceOrderAsync(IAccountProcessor accountProcessor,
                                                                         Strategy strategy,
                                                                         CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.TopSell ||
            strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.PlaceShortOrderAsync(strategy.Symbol,
                                                         strategy.Quantity,
                                                         strategy.TargetPrice,
                                                         TimeInForce.GoodTillCanceled,
                                                         ct);
        }
        else
        {
            return accountProcessor.PlaceLongOrderAsync(strategy.Symbol,
                                                        strategy.Quantity,
                                                        strategy.TargetPrice,
                                                        TimeInForce.GoodTillCanceled,
                                                        ct);
        }
    }
    private static Task<WebCallResult<BinanceOrderBase>> StopOrderAsync(IAccountProcessor accountProcessor,
                                                                        Strategy strategy,
                                                                        decimal price,
                                                                        CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.TopSell ||
            strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.StopShortOrderAsync(strategy.Symbol,
                                                        strategy.Quantity,
                                                        price,
                                                        ct);
        }
        else
        {
            return accountProcessor.StopLongOrderAsync(strategy.Symbol,
                                                       strategy.Quantity,
                                                       price,
                                                       ct);
        }
    }
    public virtual Dictionary<string, Strategy> GetMonitoringStrategy()
    {
        var strategies = _stateManager.GetState(StrategyType);
        return strategies ?? [];
    }
    public void RemoveFromMonitoringStrategy(Strategy strategy)
    {
        _stateManager.RemoveStrategy(strategy);
    }

    public virtual async Task LoadActiveStratey(CancellationToken cancellationToken)
    {
        var strategies = await _strategyRepository.FindActiveStrategyByType(StrategyType, cancellationToken);
        _stateManager.SetState(StrategyType, strategies.ToDictionary(x => x.Id));
    }

    public virtual bool ShouldStopLoss(IAccountProcessor accountProcessor,
                                       Strategy strategy,
                                       KlineClosedEvent @event)
    {
        if (string.IsNullOrEmpty(strategy.StopLossExpression))
        {
            return false;
        }
        return _javaScriptEvaluator.EvaluateExpression(strategy.StopLossExpression,
                                                       @event.Kline.OpenPrice,
                                                       @event.Kline.ClosePrice,
                                                       @event.Kline.HighPrice,
                                                       @event.Kline.LowPrice);
    }
    public virtual async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        await CheckOrderStatus(accountProcessor, strategy, ct);
        strategy.UpdatedAt = DateTime.Now;
        await _strategyRepository.UpdateAsync(strategy.Id, strategy, ct);
    }

    public virtual async Task ExecuteLoopAsync(IAccountProcessor accountProcessor,
                                              Strategy strategy,
                                              CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteAsync(accountProcessor, strategy, cancellationToken);
                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing strategy {StrategyId}", strategy.Id);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
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
                                   strategy.AccountType,
                                   strategy.Symbol,
                                   strategy.OrderId);
            strategy.HasOpenOrder = false;
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
        }
        else
        {
            _logger.LogError("[{AccountType}-{Symbol}] Failed to cancel order. Error: {ErrorMessage}",
                             strategy.AccountType,
                             strategy.Symbol,
                             cancelResult.Error?.Message);
        }
    }
    public async Task TryStopOrderAsync(IAccountProcessor accountProcessor,
                                        Strategy strategy,
                                        decimal stopPrice,
                                        CancellationToken ct)
    {
        if (strategy.OrderId is not null)
        {
            return;
        }
        var maxRetries = 3;
        var currentRetry = 0;
        var errorMessage = string.Empty;

        while (currentRetry < maxRetries)
        {
            try
            {
                var orderResult = await StopOrderAsync(accountProcessor, strategy, stopPrice, ct);

                if (orderResult.Success)
                {
                    _logger.LogInformationWithAlert("[{AccountType}-{Symbol}-{StrateType}] Triggering stop loss at price {Price}",
                                                    strategy.AccountType,
                                                    strategy.Symbol,
                                                    strategy.StrategyType,
                                                    stopPrice);
                    strategy.OrderId = null;
                    strategy.OrderPlacedTime = null;
                    strategy.HasOpenOrder = false;
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
        [{StrategyType}-{AccountType}-{Symbol}] Failed to stop order after {MaxRetries} attempts.
        StrategyId: {StrategyId}
        Error: {ErrorMessage}
        TargetPrice:{Price}, Quantity: {Quantity}.
        """, strategy.StrategyType, strategy.AccountType, strategy.Symbol, maxRetries, strategy.Id,
            errorMessage, stopPrice, strategy.Quantity);
    }

    public virtual async Task CheckOrderStatus(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        // NO open order skip check.
        if (strategy.HasOpenOrder == false || strategy.OrderId is null)
        {
            strategy.HasOpenOrder = false;
            return;
        }

        var orderResult = await accountProcessor.GetOrder(strategy.Symbol, strategy.OrderId.Value, ct);
        if (orderResult.Success)
        {
            switch (orderResult.Data.Status)
            {
                case OrderStatus.Filled:
                    _logger.LogInformationWithAlert("[{AccountType}-{Symbol}] Order filled successfully at price: {Price}.",
                                                    strategy.AccountType,
                                                    strategy.Symbol,
                                                    strategy.TargetPrice);
                    strategy.HasOpenOrder = false;
                    // Once Order filled, replace executed quantity of the order.
                    strategy.Quantity = orderResult.Data.QuantityFilled;
                    break;

                case OrderStatus.Canceled:
                case OrderStatus.Expired:
                case OrderStatus.Rejected:
                    _logger.LogInformation("[{AccountType}-{Symbol}] Order {Status}. Will try to place new order.",
                                           strategy.AccountType,
                                           strategy.Symbol,
                                           orderResult.Data.Status);
                    strategy.HasOpenOrder = false;
                    strategy.OrderId = null;
                    strategy.OrderPlacedTime = null;
                    break;
                default:
                    break;
            }
        }
        else
        {
            _logger.LogError("[{AccountType}-{Symbol}] Failed to check order status, Error: {ErrorMessage}.",
                             strategy.AccountType,
                             strategy.Symbol,
                             orderResult.Error?.Message);
        }
    }
    public virtual async Task TryPlaceOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        // OrderId is not null, no need to place order.
        if (strategy.OrderId is not null)
        {
            return;
        }
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
                    _logger.LogInformation("[{StrategyType}-{AccountType}-{Symbol}] Order placed successfully. Quantity: {Quantity}, Price: {Price}.",
                                           strategy.StrategyType,
                                           strategy.AccountType,
                                           strategy.Symbol,
                                           quantity,
                                           price);
                    strategy.OrderId = orderResult.Data.Id;
                    strategy.HasOpenOrder = true;
                    strategy.OrderPlacedTime = DateTime.UtcNow;
                    strategy.UpdatedAt = DateTime.UtcNow;
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

    public abstract Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken);
}
