using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models;
using CryptoExchange.Net.Objects;
using Trading.API.Application.Helpers;
using Trading.Domain.Entities;
using Binance.Net.Objects.Models.Spot;

namespace Trading.API.HostServices
{
    public abstract class BaseTradingService<TStrategy> : BackgroundService where TStrategy : Strategy
    {
        private readonly ILogger<BackgroundService> _logger;
        private Dictionary<string, TStrategy>? _strategies;

        protected BaseTradingService(ILogger<BackgroundService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected abstract Task<Dictionary<string, TStrategy>?> InitializeStrategies();

        protected abstract Task<WebCallResult<BinanceOrderBase>> GetOrderAsync(string symbol, long? orderId = null, string? origClientOrderId = null, long? receiveWindow = null, CancellationToken ct = default);

        protected abstract Task<WebCallResult<IEnumerable<IBinanceKline>>> GetKlinesAsync(string symbol, KlineInterval interval, DateTime? startTime = null, DateTime? endTime = null, int? limit = null, CancellationToken ct = default(CancellationToken));

        protected abstract Task<WebCallResult<BinanceOrderBase>> PlaceOrderAsync(string symbol, decimal? quantity = null, decimal? quoteQuantity = null, string? newClientOrderId = null, decimal? price = null, TimeInForce? timeInForce = null, decimal? stopPrice = null, decimal? icebergQty = null, OrderResponseType? orderResponseType = null, int? trailingDelta = null, int? strategyId = null, int? strategyType = null, SelfTradePreventionMode? selfTradePreventionMode = null, int? receiveWindow = null, CancellationToken ct = default(CancellationToken));

        protected abstract Task<WebCallResult<BinanceOrderBase>> CancelOrderAsync(string symbol, long orderId, CancellationToken ct = default(CancellationToken));
        
        protected abstract Task<bool> UpdateStrategyOrderAsync(TStrategy strategy,CancellationToken ct = default);

        protected abstract Task<(BinanceSymbolPriceFilter?, BinanceSymbolLotSizeFilter?)> GetSymbolFilterData(TStrategy strategy, CancellationToken ct = default);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _strategies = await InitializeStrategies();
                    if (_strategies != null && _strategies.Count > 0)
                    {
                        var tasks = _strategies.Values.Select(strategy => ProcessTradingPair(strategy, stoppingToken));
                        await Task.WhenAll(tasks);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "<pre>Trading strategy execution failed.</pre>");
                }
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        protected virtual async Task ProcessTradingPair(TStrategy strategy, CancellationToken stoppingToken)
        {
            try
            {
                var currentDate = DateTime.UtcNow.Date;
                if (strategy.LastTradeDate?.Date != currentDate)
                {
                    if (strategy.HasOpenOrder && strategy.OrderPlacedTime.HasValue)
                    {
                        _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] Previous day's order not filled, cancelling order before reset.</pre>",
                            strategy.StrategyType, strategy.Symbol);
                        await CancelExistingOrder(strategy, stoppingToken);
                    }
                    await ResetDailyStrategy(strategy, currentDate);
                }
                if (!strategy.IsTradedToday)
                {
                    if (strategy.HasOpenOrder)
                    {
                        await CheckOrderStatus(strategy, stoppingToken);
                    }
                    else
                    {
                        await TryPlaceOrder(strategy, stoppingToken);
                    }
                }
                strategy.UpdatedAt = DateTime.Now;
                var success =  await UpdateStrategyOrderAsync(strategy, stoppingToken);
                if (!success)
                {
                    throw new InvalidOperationException("Failed to update strategy order.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<pre>[{StrategyType}-{Symbol}] Error processing.</pre>",
                    strategy.StrategyType, strategy.Symbol);
            }
        }

        private async Task CheckOrderStatus(TStrategy strategy, CancellationToken stoppingToken)
        {
            if (strategy.OrderId is null)
            {
                strategy.HasOpenOrder = false;
                return;
            }
            var orderStatus = await GetOrderAsync(symbol: strategy.Symbol, orderId: strategy.OrderId.Value, ct: stoppingToken);
            if (orderStatus.Success)
            {
                switch (orderStatus.Data.Status)
                {
                    case OrderStatus.Filled:
                        _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] Order filled successfully at price: {Price}.</pre>",
                            strategy.StrategyType, strategy.Symbol, strategy.TargetPrice);
                        strategy.IsTradedToday = true;
                        strategy.HasOpenOrder = false;
                        strategy.OrderId = null;
                        strategy.OrderPlacedTime = null;
                        break;

                    case OrderStatus.Canceled:
                    case OrderStatus.Expired:
                    case OrderStatus.Rejected:
                        _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] Order {Status}. Will try to place new order.</pre>",
                            strategy.StrategyType, strategy.Symbol, orderStatus.Data.Status);
                        strategy.HasOpenOrder = false;
                        strategy.OrderId = null;
                        strategy.OrderPlacedTime = null;
                        break;
                    default:
                        if (strategy.OrderPlacedTime.HasValue && strategy.OrderPlacedTime.Value.Date != DateTime.UtcNow.Date)
                        {
                            _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] Order from previous day detected, initiating cancellation.</pre>",
                                strategy.StrategyType, strategy.Symbol);
                            await CancelExistingOrder(strategy, stoppingToken);
                        }
                        break;
                }
            }
            else
            {
                _logger.LogError("<pre>[{StrategyType}-{Symbol}] Failed to check order status, Error: {ErrorMessage}.</pre>",
                    strategy.StrategyType, strategy.Symbol, orderStatus.Error?.Message);
            }
        }


        private async Task CancelExistingOrder(TStrategy strategy, CancellationToken stoppingToken)
        {
            if (strategy.OrderId is null) return;
            try
            {

                var cancelResult = await CancelOrderAsync(strategy.Symbol, strategy.OrderId.Value, ct: stoppingToken);
                if (cancelResult.Success)
                {
                    _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] Successfully cancelled order, OrderId: {OrderId}</pre>",
                        strategy.StrategyType, strategy.Symbol, strategy.OrderId);
                    strategy.HasOpenOrder = false;
                    strategy.OrderId = null;
                    strategy.OrderPlacedTime = null;
                }
                else
                {
                    _logger.LogError("<pre>[{StrategyType}-{Symbol}] Failed to cancel order. Error: {ErrorMessage}</pre>",
                        strategy.StrategyType, strategy.Symbol, cancelResult.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<pre>[{StrategyType}-{Symbol}] Error while cancelling order {OrderId}.</pre>",
                    strategy.StrategyType, strategy.Symbol, strategy.OrderId);
            }
        }

        private async Task ResetDailyStrategy(TStrategy strategy, DateTime currentDate)
        {
            var kLines = await GetKlinesAsync(strategy.Symbol, KlineInterval.OneDay, startTime: currentDate, limit: 1);
            if (kLines.Success && kLines.Data.Any())
            {
                var openPrice = CommonHelper.TrimEndZero(kLines.Data.First().OpenPrice);
                var filterData = await GetSymbolFilterData(strategy);
                strategy.TargetPrice = CommonHelper.AdjustPriceByStepSize(openPrice * (1 - strategy.PriceDropPercentage), filterData.Item1);
                strategy.Quantity = CommonHelper.AdjustQuantityBystepSize(strategy.Amount / strategy.TargetPrice, filterData.Item2);
                strategy.LastTradeDate = currentDate;
                strategy.IsTradedToday = false;
                strategy.HasOpenOrder = false;
                strategy.OrderId = null;
                strategy.OrderPlacedTime = null;
                _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] New day started, Open price: {OpenPrice}, Target price: {TargetPrice}.</pre>",
                    strategy.StrategyType, strategy.Symbol, openPrice, strategy.TargetPrice);
            }
            else
            {
                _logger.LogError("<pre>[{StrategyType}-{Symbol}] Failed to get daily open price. Error: {ErrorMessage}.</pre>",
                    strategy.StrategyType, strategy.Symbol, kLines.Error?.Message);
            }
        }

        private async Task TryPlaceOrder(TStrategy strategy, CancellationToken stoppingToken)
        {
            var quantity = CommonHelper.TrimEndZero(strategy.Quantity);
            var price = CommonHelper.TrimEndZero(strategy.TargetPrice);
            var orderResult = await PlaceOrderAsync(symbol: strategy.Symbol,
                quantity: quantity,
                price: price,
                timeInForce: TimeInForce.GoodTillCanceled,
                ct: stoppingToken);
            if (orderResult.Success)
            {
                _logger.LogInformation("<pre>[{StrategyType}-{Symbol}] Order placed successfully. Quantity: {Quantity}, Price: {Price}.</pre>",
                    strategy.StrategyType, strategy.Symbol, quantity, price);
                strategy.OrderId = orderResult.Data.Id;
                strategy.HasOpenOrder = true;
                strategy.OrderPlacedTime = DateTime.UtcNow;
            }
            else
            {
                _logger.LogError("<pre>[{StrategyType}-{Symbol}] Failed to place order. Error: {ErrorMessage}, TargetPrice:{Price}, Quantity: {Quantity}.</pre>",
                    strategy.StrategyType, strategy.Symbol, orderResult.Error?.Message, price, quantity);
            }
        }
    }
}