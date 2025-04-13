using Binance.Net.Clients;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;

namespace Trading.API.Services.Alerts;

public class KlineStreamManager : IDisposable
{
    private readonly ILogger<KlineStreamManager> _logger;
    private readonly BinanceSocketClient _socketClient;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromHours(23);
    private DateTime _lastConnectionTime = DateTime.UtcNow;
    private UpdateSubscription? _subscription;

    public event Action<string, IBinanceKline>? OnKlineUpdate;

    public KlineStreamManager(
        ILogger<KlineStreamManager> logger,
        BinanceSocketClient socketClient)
    {
        _logger = logger;
        _socketClient = socketClient;
    }

    public async Task SubscribeSymbols(HashSet<string> symbols, CancellationToken ct)
    {
        await CloseExistingSubscription();

        if (symbols.Count == 0)
        {
            return;
        }

        try
        {
            var result = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbols.ToList(),
                Binance.Net.Enums.KlineInterval.FourHour,
                HandlePriceUpdate,
                ct: ct);

            if (!result.Success)
            {
                _logger.LogError("Failed to subscribe: {@Error}", result.Error);
                return;
            }

            _subscription = result.Data;
            _lastConnectionTime = DateTime.UtcNow;

            _logger.LogInformation("Subscribed to {Count} symbols: {Symbols}",
                symbols.Count, string.Join(", ", symbols));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscription failed");
            throw;
        }
    }

    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }

        OnKlineUpdate?.Invoke(data.Data.Symbol, data.Data.Data);
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
}
