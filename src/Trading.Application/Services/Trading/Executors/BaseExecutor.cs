using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using CryptoExchange.Net.Objects;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Common.Helpers;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public abstract class BaseExecutor :
    INotificationHandler<KlineClosedEvent>
{
    protected readonly ILogger _logger;
    protected readonly IStrategyRepository _strategyRepository;
    protected readonly JavaScriptEvaluator _javaScriptEvaluator;
    protected readonly IStrategyStateManager _stateManager;
    protected readonly IAccountProcessorFactory _accountProcessorFactory;
    public BaseExecutor(ILogger logger,
                        IStrategyRepository strategyRepository,
                        JavaScriptEvaluator javaScriptEvaluator,
                        IAccountProcessorFactory accountProcessorFactory,
                        IStrategyStateManager strategyStateManager)
    {
        _strategyRepository = strategyRepository;
        _javaScriptEvaluator = javaScriptEvaluator;
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _stateManager = strategyStateManager;
    }

    public abstract StrategyType StrategyType { get; }
    private static Task<WebCallResult<BinanceOrderBase>> PlaceOrderAsync(IAccountProcessor accountProcessor,
                                                                         Strategy strategy,
                                                                         CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.TopSell || strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.PlaceShortOrderAsync(strategy.Symbol, strategy.Quantity, strategy.TargetPrice, TimeInForce.GoodTillCanceled, ct);
        }
        return accountProcessor.PlaceLongOrderAsync(strategy.Symbol, strategy.Quantity, strategy.TargetPrice, TimeInForce.GoodTillCanceled, ct);
    }
    private static Task<WebCallResult<BinanceOrderBase>> StopOrderAsync(IAccountProcessor accountProcessor,
                                                                        Strategy strategy,
                                                                        decimal price,
                                                                        CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.TopSell || strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.StopShortOrderAsync(strategy.Symbol, strategy.Quantity, price, ct);
        }
        return accountProcessor.StopLongOrderAsync(strategy.Symbol, strategy.Quantity, price, ct);
    }
    private async Task<(bool, WebCallResult<T>?)> ExecuteWithRetry<T>(Func<Task<WebCallResult<T>>> operation, Strategy strategy, CancellationToken ct)
    {
        var MAX_RETRIES = 1;
        WebCallResult<T>? result = null;
        for (var attempt = 0; attempt < MAX_RETRIES; attempt++)
        {
            result = await operation();
            if (result.Success)
            {
                return (true, result);
            }
            if (attempt < MAX_RETRIES - 1)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning(
                    "[{StrategyType}-{AccountType}-{Symbol}] Attempt {RetryCount} of {MaxRetries} failed. Retrying in {Delay} seconds. Error: {Error}",
                    strategy.StrategyType,
                    strategy.AccountType,
                    strategy.Symbol,
                    attempt + 1,
                    MAX_RETRIES,
                    delay.TotalSeconds,
                    result.Error?.Message);
                await Task.Delay(delay, ct);
            }
        }

        return (false, result);
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

    public virtual async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        await CheckOrderStatus(accountProcessor, strategy, ct);
        strategy.UpdatedAt = DateTime.Now;
        await _strategyRepository.UpdateAsync(strategy.Id, strategy, ct);
    }

    public virtual async Task ExecuteLoopAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken cancellationToken)
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

        var cancelResult = await accountProcessor.CancelOrderAsync(strategy.Symbol, strategy.OrderId.Value, ct);
        if (cancelResult.Success)
        {
            _logger.LogInformation("[{AccountType}-{Symbol}] Successfully cancelled order, OrderId: {OrderId}",
                                   strategy.AccountType,
                                   strategy.Symbol,
                                   strategy.OrderId);
            strategy.HasOpenOrder = false;
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
            return;
        }
        _logger.LogError("[{AccountType}-{Symbol}] Failed to cancel order. Error: {ErrorMessage}",
                         strategy.AccountType,
                         strategy.Symbol,
                         cancelResult.Error?.Message);
    }
    public async Task TryStopOrderAsync(IAccountProcessor accountProcessor, Strategy strategy, decimal stopPrice, CancellationToken ct)
    {
        if (strategy.OrderId is null)
        {
            return;
        }
        var (success, result) = await ExecuteWithRetry(() => StopOrderAsync(accountProcessor, strategy, stopPrice, ct), strategy, ct);
        if (success)
        {
            _logger.LogInformationWithAlert(
                "[{AccountType}-{Symbol}-{StrateType}] Triggering stop loss at price {Price}",
                strategy.AccountType,
                strategy.Symbol,
                strategy.StrategyType,
                stopPrice);
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
            strategy.HasOpenOrder = false;
            return;
        }
        _logger.LogErrorWithAlert("""
        [{StrategyType}-{AccountType}-{Symbol}] Failed to stop order.
        StrategyId: {StrategyId}
        Error: {ErrorMessage}
        TargetPrice:{Price}, Quantity: {Quantity}.
        """, strategy.StrategyType, strategy.AccountType, strategy.Symbol, strategy.Id,
            result?.Error?.Message, stopPrice, strategy.Quantity);
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
            return;
        }
        _logger.LogError("[{AccountType}-{Symbol}] Failed to check order status, Error: {ErrorMessage}.",
                         strategy.AccountType,
                         strategy.Symbol,
                         orderResult.Error?.Message);
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
        // always set order placed time when placing order.
        strategy.OrderPlacedTime = DateTime.UtcNow;

        var (success, result) = await ExecuteWithRetry(() => PlaceOrderAsync(accountProcessor, strategy, ct), strategy, ct);
        if (success)
        {
            _logger.LogInformation("[{StrategyType}-{AccountType}-{Symbol}] Order placed successfully. Quantity: {Quantity}, Price: {Price}.",
                                   strategy.StrategyType,
                                   strategy.AccountType,
                                   strategy.Symbol,
                                   strategy.Quantity,
                                   strategy.TargetPrice);
            strategy.OrderId = result!.Data.Id;
            strategy.HasOpenOrder = true;
            strategy.UpdatedAt = DateTime.UtcNow;
            return;
        }

        _logger.LogErrorWithAlert("""
        [{StrategyType}-{AccountType}-{Symbol}] Failed to place order.
        StrategyId: {StrategyId}
        Error: {ErrorMessage}
        TargetPrice:{Price}, Quantity: {Quantity}.
        """, strategy.StrategyType, strategy.AccountType, strategy.Symbol, strategy.Id,
            result?.Error?.Message, price, quantity);
    }

    public virtual async Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        var strategies = GetMonitoringStrategy().Values.Where(x => x.Symbol == notification.Symbol
                        && x.Interval == BinanceHelper.ConvertToIntervalString(notification.Interval));
        var tasks = strategies.Select(async strategy =>
        {
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            if (accountProcessor != null)
            {
                await HandleKlineClosedEvent(accountProcessor, strategy, notification, cancellationToken);
                if (ShouldStopLoss(accountProcessor, strategy, notification))
                {
                    await TryStopOrderAsync(accountProcessor, strategy, notification.Kline.ClosePrice, cancellationToken);
                    strategy.Pause();
                }
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
            }
        });
        await Task.WhenAll(tasks);
    }
    public virtual Task HandleKlineClosedEvent(IAccountProcessor accountProcessor, Strategy strategy, KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    protected bool ShouldStopLoss(IAccountProcessor accountProcessor, Strategy strategy, KlineClosedEvent @event)
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
}
