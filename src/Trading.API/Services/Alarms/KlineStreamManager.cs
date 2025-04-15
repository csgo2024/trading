using Binance.Net.Clients;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Trading.Domain.Events;

namespace Trading.API.Services.Alarms;

public class KlineUpdateEvent : INotification
{
    public string Symbol { get; }
    public IBinanceKline Kline { get; }

    public KlineUpdateEvent(string symbol, IBinanceKline kline)
    {
        Symbol = symbol;
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

    public KlineStreamManager(
        ILogger<KlineStreamManager> logger,
        IMediator mediator,
        BinanceSocketClient socketClient)
    {
        _logger = logger;
        _mediator = mediator;
        _socketClient = socketClient;
    }

    public async Task SubscribeSymbols(HashSet<string> symbols, CancellationToken ct)
    {
        if (symbols.Count == 0)
        {
            return;
        }

        if (symbols.IsSubsetOf(_listenedSymbols))
        {
            return;
        }

        await CloseExistingSubscription();

        try
        {
            _listenedSymbols.UnionWith(symbols);
            var result = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                _listenedSymbols.ToArray(),
                Binance.Net.Enums.KlineInterval.FourHour,
                HandlePriceUpdate,
                ct: ct);

            if (!result.Success)
            {
                _listenedSymbols.UnionWith(symbols);
                _logger.LogError("Failed to subscribe: {@Error}", result.Error);
                return;
            }
            _subscription = result.Data;
            _lastConnectionTime = DateTime.UtcNow;

            _logger.LogInformation("Subscribed to {Count} symbols: {@Symbols}",
                _listenedSymbols.Count, _listenedSymbols);
        }
        catch (Exception ex)
        {
            _listenedSymbols.Clear();
            _logger.LogError(ex, "Subscription failed");
        }
    }

    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }

        Task.Run(() => _mediator.Publish(new KlineUpdateEvent(data.Data.Symbol, data.Data.Data)));
    }

    public bool NeedsReconnection() => DateTime.UtcNow - _lastConnectionTime > _reconnectInterval;

    private async Task CloseExistingSubscription()
    {
        if (_subscription != null)
        {
            try
            {
                await _subscription.CloseAsync();
                _subscription = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing subscription");
            }
        }
    }

    public void Dispose()
    {
        _subscription?.CloseAsync().Wait();
    }

    public async Task Handle(AlarmResumedEvent notification, CancellationToken cancellationToken)
    {
        await SubscribeSymbols([notification.Alarm.Symbol], cancellationToken);
    }

    public async Task Handle(AlarmCreatedEvent notification, CancellationToken cancellationToken)
    {
        await SubscribeSymbols([notification.Alarm.Symbol], cancellationToken);
    }
}
