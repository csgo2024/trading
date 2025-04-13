using Binance.Net.Clients;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class PriceAlertService : BackgroundService
{
    private readonly ILogger<PriceAlertService> _logger;
    private readonly BinanceSocketClient _socketClient;
    private readonly IPriceAlertRepository _alertRepository;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    private readonly List<PriceAlert> _activeAlerts;
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromHours(23); // 23小时重连一次
    private DateTime _lastConnectionTime = DateTime.UtcNow;
    private UpdateSubscription? _subscription;

    public PriceAlertService(
        ILogger<PriceAlertService> logger,
        BinanceSocketClient socketClient,
        IPriceAlertRepository alertRepository,
        ITelegramBotClient botClient,
        IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _socketClient = socketClient;
        _alertRepository = alertRepository;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _activeAlerts = new List<PriceAlert>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await LoadAndSubscribe(stoppingToken);
                // 每分钟检查一次新的警告
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Price alert service error");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscription != null)
        {
            await _subscription.CloseAsync();
        }
        await base.StopAsync(cancellationToken);
    }

    private async Task LoadAndSubscribe(CancellationToken stoppingToken)
    {
        await _subscriptionLock.WaitAsync(stoppingToken);
        try
        {
            // 1. 加载数据库中的警告
            var dbAlerts = await _alertRepository.GetActiveAlertsAsync(stoppingToken);
            var dbSymbols = dbAlerts.Select(a => a.Symbol).Distinct().ToHashSet();
            var memorySymbols = _activeAlerts.Select(a => a.Symbol).Distinct().ToHashSet();

            // 2. 检查是否需要重新连接
            if (DateTime.UtcNow - _lastConnectionTime > _reconnectInterval)
            {
                _activeAlerts.Clear();
                _activeAlerts.AddRange(dbAlerts);
                _logger.LogInformation("Reconnecting WebSocket after 23 hours...");
                await ResubscribeWebSocket(dbSymbols, stoppingToken);
                return;
            }

            // 3. 检查是否需要更新订阅
            if (dbSymbols.Count == 0)
            {
                await CloseExistingSubscription();
                _activeAlerts.Clear();
                return;
            }

            // 如果币种没有变化，只更新内存中的警告
            if (memorySymbols.SetEquals(dbSymbols))
            {
                _activeAlerts.Clear();
                _activeAlerts.AddRange(dbAlerts);
                return;
            }

            // 4. 更新内存中的警告列表
            _logger.LogInformation("Symbols changed from {Old} to {New}",
                string.Join(",", memorySymbols),
                string.Join(",", dbSymbols));

            _activeAlerts.Clear();
            _activeAlerts.AddRange(dbAlerts);

            // 4. 重新订阅WebSocket
            await ResubscribeWebSocket(dbSymbols, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load and subscribe alerts");
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

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
                _logger.LogError(ex, "Error closing existing subscription");
            }
        }
    }

    private async Task ResubscribeWebSocket(HashSet<string> symbols, CancellationToken stoppingToken)
    {
        // 1. 关闭现有订阅
        await CloseExistingSubscription();

        if (symbols.Count == 0)
        {
            return;
        }

        // 2. 创建新的WebSocket订阅
        try
        {
            var result = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbols.ToList(),
                Binance.Net.Enums.KlineInterval.FourHour, // 使用4小时K线
                HandlePriceUpdate,
                ct: stoppingToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to subscribe to price updates: {@Error}", result.Error);
                return;
            }

            _subscription = result.Data;
            _lastConnectionTime = DateTime.UtcNow;

            _logger.LogInformation("Successfully subscribed to {Count} symbols: {Symbols}",
                symbols.Count,
                string.Join(", ", symbols));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new subscription");
        }
    }

    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }

        var symbol = data.Data.Symbol;
        var price = data.Data.Data.ClosePrice;

        // 查找所有匹配的警告
        var matchingAlerts = _activeAlerts
            .Where(alert => alert.Symbol == symbol && alert.IsActive)
            .Where(alert =>
                (alert.Type == AlertType.Above && price > alert.TargetPrice) ||
                (alert.Type == AlertType.Below && price < alert.TargetPrice))
            .Where(alert => (DateTime.UtcNow - alert.LastNotification).TotalSeconds >= 30)
            .ToList();

        foreach (var alert in matchingAlerts)
        {
            SendAlert(alert, price);
        }
    }

    private async void SendAlert(PriceAlert alert, decimal currentPrice)
    {
        try
        {
            alert.LastNotification = DateTime.UtcNow;
            await _alertRepository.UpdateAsync(alert.Id, alert);

            var keyboard = new InlineKeyboardMarkup([
                [
                    InlineKeyboardButton.WithCallbackData("暂停", $"pause_{alert.Id}"),
                    InlineKeyboardButton.WithCallbackData("恢复", $"resume_{alert.Id}")
                ]
            ]);

            await _botClient.SendMessage(
                chatId: _chatId,
                text: $"<pre>⚠️ {alert.Symbol}\n类型:{alert.Type}\n当前价格: {currentPrice}\n目标价格: {alert.TargetPrice}</pre>",
                parseMode: ParseMode.Html,
                replyMarkup: keyboard
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }

}
