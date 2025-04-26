using Binance.Net.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Helpers;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Trading.Account;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Trading.Executors;

public class CloseBuyExecutor :
    INotificationHandler<KlineClosedEvent>
{
    private readonly ILogger<CloseBuyExecutor> _logger;
    private readonly IStrategyRepository _strategyRepository;

    private readonly IAccountProcessorFactory _accountProcessorFactory;

    public CloseBuyExecutor(ILogger<CloseBuyExecutor> logger,
                            IAccountProcessorFactory accountProcessorFactory,
                            IStrategyRepository strategyRepository)
    {
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _strategyRepository = strategyRepository;
    }

    public async Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        var aaa = await _strategyRepository.Find(notification.Symbol,
                                                 CommonHelper.ConvertToIntervalString(notification.Interval),
                                                 StrategyType.CloseBuy,
                                                 cancellationToken);
        foreach (var strategy in aaa)
        {
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            if (accountProcessor != null)
            {
                var filterData = await accountProcessor.GetSymbolFilterData(strategy, cancellationToken);
                var closePrice = notification.Kline.ClosePrice;
                strategy.TargetPrice = CommonHelper.AdjustPriceByStepSize(closePrice * (1 - strategy.Volatility), filterData.Item1);
                strategy.Quantity = CommonHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
                if (strategy.HasOpenOrder)
                {
                    continue;
                }
                else
                {
                    await TryPlaceOrder(accountProcessor, strategy, cancellationToken);

                }
                strategy.UpdatedAt = DateTime.UtcNow;
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
            }
        }
    }
    public async Task TryPlaceOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var quantity = CommonHelper.TrimEndZero(strategy.Quantity);
        var price = CommonHelper.TrimEndZero(strategy.TargetPrice);
        var maxRetries = 3;
        var currentRetry = 0;
        var errorMessage = string.Empty;

        while (currentRetry < maxRetries)
        {
            try
            {
                var orderResult = await accountProcessor.PlaceLongOrderAsync(
                    strategy.Symbol,
                    quantity,
                    price,
                    TimeInForce.GoodTillCanceled,
                    ct);

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
