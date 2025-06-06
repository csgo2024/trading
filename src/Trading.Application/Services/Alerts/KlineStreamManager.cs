using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Domain.Events;
using Trading.Exchange.Binance.Helpers;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Services.Alerts;

public class KlineClosedEvent : INotification
{
    public string Symbol { get; }
    public KlineInterval Interval { get; }
    public IBinanceKline Kline { get; }

    public KlineClosedEvent(string symbol, KlineInterval interval, IBinanceKline kline)
    {
        Symbol = symbol;
        Interval = interval;
        Kline = kline;
    }
}

public interface IKlineStreamManager : IDisposable,
    INotificationHandler<AlertResumedEvent>,
    INotificationHandler<AlertCreatedEvent>,
    INotificationHandler<StrategyCreatedEvent>
{
    Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct);
    bool NeedsReconnection();
}

public class KlineStreamManager : IKlineStreamManager
{
    private DateTime _lastConnectionTime = DateTime.UtcNow;
    private UpdateSubscription? _subscription;
    private readonly BinanceSocketClientUsdFuturesApiWrapper _usdFutureSocketClient;
    private readonly ILogger<KlineStreamManager> _logger;
    private readonly IMediator _mediator;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromMinutes(12 * 60);
    private static readonly HashSet<string> _listenedIntervals = [];
    private static readonly HashSet<string> _listenedSymbols = [];

    public KlineStreamManager(ILogger<KlineStreamManager> logger,
                              IMediator mediator,
                              BinanceSocketClientUsdFuturesApiWrapper usdFutureSocketClient)
    {
        _logger = logger;
        _mediator = mediator;
        _usdFutureSocketClient = usdFutureSocketClient;
    }

    public async Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct)
    {
        if (symbols.Count == 0 || intervals.Count == 0)
        {
            return false;
        }

        await CloseExistingSubscription();

        var mergedSymbols = new HashSet<string>(_listenedSymbols);
        mergedSymbols.UnionWith(symbols);
        var mergedIntervals = new HashSet<string>(_listenedIntervals);
        mergedIntervals.UnionWith(intervals);
        var result = await _usdFutureSocketClient.ExchangeData.SubscribeToKlineUpdatesAsync(
            mergedSymbols,
            mergedIntervals.Select(BinanceHelper.ConvertToKlineInterval),
            HandlePriceUpdate,
            ct: ct);

        if (!result.Success)
        {
            _logger.LogError("Failed to subscribe: {@Error}", result.Error);
            return false;
        }
        _listenedSymbols.UnionWith(mergedSymbols);
        _listenedIntervals.UnionWith(mergedIntervals);
        _subscription = result.Data;
        _lastConnectionTime = DateTime.UtcNow;

        // SubscribeToEvents(_subscription);
        _logger.LogInformation("Subscribed to {Count} symbols: {@Symbols} intervals: {@Intervals}",
                               _listenedSymbols.Count,
                               _listenedSymbols,
                               _listenedIntervals);
        return true;
    }

    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }

        Task.Run(() => _mediator.Publish(new KlineClosedEvent(data.Data.Symbol, data.Data.Data.Interval, data.Data.Data)));
    }

    private async Task CloseExistingSubscription()
    {
        if (_subscription != null)
        {
            try
            {
                // UnsubscribeEvents(_subscription);
                await _subscription.CloseAsync();
                _subscription = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing subscription");
            }
        }
    }
    public bool NeedsReconnection() => DateTime.UtcNow - _lastConnectionTime > _reconnectInterval;

    public void Dispose()
    {
        _subscription?.CloseAsync().Wait();
    }

    public async Task Handle(AlertResumedEvent notification, CancellationToken cancellationToken)
    {
        await SubscribeSymbols([notification.Alert.Symbol], [notification.Alert.Interval], cancellationToken);
    }

    public async Task Handle(AlertCreatedEvent notification, CancellationToken cancellationToken)
    {
        await SubscribeSymbols([notification.Alert.Symbol], [notification.Alert.Interval], cancellationToken);
    }

    public async Task Handle(StrategyCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Strategy.Symbol != null && notification.Strategy.Interval != null)
        {
            await SubscribeSymbols([notification.Strategy.Symbol], [notification.Strategy.Interval], cancellationToken);
        }
    }
}
