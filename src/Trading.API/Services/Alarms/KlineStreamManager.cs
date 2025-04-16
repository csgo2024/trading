using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Trading.Application.Helpers;
using Trading.Domain.Events;

namespace Trading.API.Services.Alarms;

public class KlineUpdateEvent : INotification
{
    public string Symbol { get; }
    public KlineInterval Interval { get; }
    public IBinanceKline Kline { get; }

    public KlineUpdateEvent(string symbol, KlineInterval interval, IBinanceKline kline)
    {
        Symbol = symbol;
        Interval = interval;
        Kline = kline;
    }
}

public class KlineStreamManager : IDisposable,
    INotificationHandler<AlarmResumedEvent>,
    INotificationHandler<AlarmCreatedEvent>
{
    private readonly ILogger<KlineStreamManager> _logger;
    private readonly BinanceSocketClient _socketClient;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromHours(23);
    private DateTime _lastConnectionTime = DateTime.UtcNow;
    private UpdateSubscription? _subscription;

    private readonly IMediator _mediator;

    private static readonly HashSet<string> _listenedSymbols = [];
    private static readonly HashSet<string> _listenedIntervals = [];

    public KlineStreamManager(
        ILogger<KlineStreamManager> logger,
        IMediator mediator,
        BinanceSocketClient socketClient)
    {
        _logger = logger;
        _mediator = mediator;
        _socketClient = socketClient;
    }

    public async Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct)
    {
        if (symbols.Count == 0 || intervals.Count == 0)
        {
            return false;
        }

        if (symbols.IsSubsetOf(_listenedSymbols) && intervals.IsSubsetOf(_listenedIntervals))
        {
            return true;
        }

        await CloseExistingSubscription();

        var mergedSymbols = new HashSet<string>(_listenedSymbols);
        mergedSymbols.UnionWith(symbols);
        var mergedIntervals = new HashSet<string>(_listenedIntervals);
        mergedIntervals.UnionWith(intervals);
        var result = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
            mergedSymbols.ToArray(),
            mergedIntervals.Select(CommonHelper.ConvertToKlineInterval).ToArray(),
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
        SubscribeToEvents(_subscription);
        _lastConnectionTime = DateTime.UtcNow;

        _logger.LogInformation("Subscribed to {Count} symbols: {@Symbols} intervals: {@Intervals}",
            _listenedSymbols.Count, _listenedSymbols, _listenedIntervals);
        return true;
    }

    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }

        Task.Run(() => _mediator.Publish(new KlineUpdateEvent(data.Data.Symbol, data.Data.Data.Interval, data.Data.Data)));
    }

    private void OnConnectionLost()
    {
        _logger.LogWarning("WebSocket connection lost for symbols: {@Symbols}", _listenedSymbols);
    }

    private void OnConnectionRestored(TimeSpan timeSpan)
    {
        _logger.LogInformation("Connection restored after {Delay}ms for symbols: {@Symbols}",
            timeSpan.TotalMilliseconds,
            _listenedSymbols);
    }

    private void OnConnectionClosed()
    {
        _logger.LogInformation("Connection closed for symbols: {@Symbols}", _listenedSymbols);
    }

    private void OnResubscribingFailed(Error error)
    {
        _logger.LogError("Resubscribing failed for symbols: {@Symbols}, Error: {@Error}",
            _listenedSymbols,
            error);
    }

    private void OnActivityPaused()
    {
        _logger.LogWarning("Connection activity paused for symbols: {@Symbols}", _listenedSymbols);
    }

    private void OnActivityUnpaused()
    {
        _logger.LogInformation("Connection activity resumed for symbols: {@Symbols}", _listenedSymbols);
    }

    private void OnException(Exception exception)
    {
        _logger.LogError(exception, "Exception occurred for symbols: {@Symbols}", _listenedSymbols);
    }

    private void SubscribeToEvents(UpdateSubscription subscription)
    {
        subscription.ConnectionLost += OnConnectionLost;
        subscription.ConnectionRestored += OnConnectionRestored;
        subscription.ConnectionClosed += OnConnectionClosed;
        subscription.ResubscribingFailed += OnResubscribingFailed;
        subscription.ActivityPaused += OnActivityPaused;
        subscription.ActivityUnpaused += OnActivityUnpaused;
        subscription.Exception += OnException;
    }

    private void UnsubscribeEvents(UpdateSubscription subscription)
    {
        subscription.ConnectionLost -= OnConnectionLost;
        subscription.ConnectionRestored -= OnConnectionRestored;
        subscription.ConnectionClosed -= OnConnectionClosed;
        subscription.ResubscribingFailed -= OnResubscribingFailed;
        subscription.ActivityPaused -= OnActivityPaused;
        subscription.ActivityUnpaused -= OnActivityUnpaused;
        subscription.Exception -= OnException;
    }

    private async Task CloseExistingSubscription()
    {
        if (_subscription != null)
        {
            try
            {
                UnsubscribeEvents(_subscription);
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

    public async Task Handle(AlarmResumedEvent notification, CancellationToken cancellationToken)
    {
        await SubscribeSymbols([notification.Alarm.Symbol], [notification.Alarm.Interval], cancellationToken);
    }

    public async Task Handle(AlarmCreatedEvent notification, CancellationToken cancellationToken)
    {
        await SubscribeSymbols([notification.Alarm.Symbol], [notification.Alarm.Interval], cancellationToken);
    }
}
