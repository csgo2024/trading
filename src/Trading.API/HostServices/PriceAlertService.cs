using System.Collections.Concurrent;
using Binance.Net.Clients;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using Jint;
using Jint.Runtime;
using MediatR;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class PriceAlertService : BackgroundService, INotificationHandler<AlertStatusChangedEvent>
{
    private readonly ILogger<PriceAlertService> _logger;
    private readonly BinanceSocketClient _socketClient;
    private readonly IPriceAlertRepository _alertRepository;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    private static readonly ConcurrentDictionary<string, PriceAlert> _activeAlerts = new();
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromHours(23); // 23小时重连一次
    private DateTime _lastConnectionTime = DateTime.UtcNow;
    private UpdateSubscription? _subscription;

    private readonly Engine _jsEngine;
    private readonly Dictionary<string, IBinanceKline> _lastKlines = new();
    private readonly ConcurrentDictionary<string, Task> _alertTasks = new();
    private readonly CancellationTokenSource _cts = new();

    public PriceAlertService(
        ILogger<PriceAlertService> logger,
        BinanceSocketClient socketClient,
        IPriceAlertRepository alertRepository,
        ITelegramBotClient botClient,
        IOptions<TelegramSettings> settings,
        IMediator mediator)
    {
        _logger = logger;
        _socketClient = socketClient;
        _alertRepository = alertRepository;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _jsEngine = new Engine(cfg => cfg
            .LimitRecursion(10)
            .MaxStatements(50)
            .TimeoutInterval(TimeSpan.FromSeconds(10))
        );
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
        try
        {
            _cts.Cancel();
            await Task.WhenAll(_alertTasks.Values);
        }
        finally
        {
            if (_subscription != null)
            {
                await _subscription.CloseAsync();
            }
            await base.StopAsync(cancellationToken);
        }
    }

    private bool EvaluateCondition(string condition, decimal open, decimal close, decimal high, decimal low)
    {
        try
        {
            // 注入价格数据
            _jsEngine.SetValue("open", (double)open);
            _jsEngine.SetValue("close", (double)close);
            _jsEngine.SetValue("high", (double)high);
            _jsEngine.SetValue("low", (double)low);

            // 执行条件代码
            var result = _jsEngine.Evaluate(condition);
            return result.AsBoolean();
        }
        catch (JavaScriptException ex)
        {
            _logger.LogError(ex, "JavaScript evaluation error for condition: {Condition}", condition);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition: {Condition}", condition);
            return false;
        }
    }
    private async Task LoadAndSubscribe(CancellationToken stoppingToken)
    {
        await _subscriptionLock.WaitAsync(stoppingToken);
        try
        {
            // 1. 加载数据库中的警告
            var dbAlerts = await _alertRepository.GetActiveAlertsAsync(stoppingToken);
            var dbSymbols = dbAlerts.Select(a => a.Symbol).Distinct().ToHashSet();
            var memorySymbols = _activeAlerts.Values.Select(x => x.Symbol).ToHashSet();
            // 2. 检查是否需要重新连接
            if (DateTime.UtcNow - _lastConnectionTime > _reconnectInterval)
            {
                foreach (var alert in dbAlerts)
                {
                    _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
                }
                _logger.LogInformation("Reconnecting WebSocket after 23 hours...");
                await ResubscribeWebSocket(dbSymbols, stoppingToken);
                return;
            }

            // 3. 检查是否需要更新订阅
            if (dbSymbols.Count == 0)
            {
                await CloseExistingSubscription();
                return;
            }

            // 如果币种没有变化，只更新内存中的警告
            if (memorySymbols.SetEquals(dbSymbols))
            {
                foreach (var alert in dbAlerts)
                {
                    _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
                }
                return;
            }

            // 4. 更新内存中的警告列表
            _logger.LogInformation("Symbols changed from {Old} to {New}",
                string.Join(",", memorySymbols),
                string.Join(",", dbSymbols));

            foreach (var alert in dbAlerts)
            {
                _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
            }

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
            var result = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToKlineUpdatesAsync(
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
        var kline = data.Data.Data;

        // 更新最新K线数据
        _lastKlines[symbol] = kline;

        // 如果该币种还没有告警检查任务，则创建一个
        if (!_alertTasks.ContainsKey(symbol))
        {
            var task = StartAlertCheckLoop(symbol);
            _alertTasks.TryAdd(symbol, task);
        }
    }

    private async Task StartAlertCheckLoop(string symbol)
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_lastKlines.TryGetValue(symbol, out var kline))
                {
                    var matchingAlerts = _activeAlerts.Values
                        .Where(alert => alert.Symbol == symbol && alert.IsActive)
                        .Where(alert => (DateTime.UtcNow - alert.LastNotification).TotalSeconds >= 30)
                        .Where(alert => EvaluateCondition(
                            alert.Condition,
                            kline.OpenPrice,
                            kline.ClosePrice,
                            kline.HighPrice,
                            kline.LowPrice
                        ))
                        .ToList();

                    foreach (var alert in matchingAlerts)
                    {
                        SendAlert(alert, kline);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alerts for {Symbol}", symbol);
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
            }
        }
    }

    private async void SendAlert(PriceAlert alert, IBinanceKline kline)
    {
        try
        {
            alert.LastNotification = DateTime.UtcNow;
            await _alertRepository.UpdateAsync(alert.Id, alert);

            // 计算涨跌幅
            var priceChange = kline.ClosePrice - kline.OpenPrice;
            var priceChangePercent = priceChange / kline.OpenPrice * 100;
            var trend = priceChange >= 0 ? "📈" : "📉";
            var changeText = priceChange >= 0 ? "🟢 上涨" : "🔴 下跌";

            var keyboard = new InlineKeyboardMarkup([
                [
                    InlineKeyboardButton.WithCallbackData("暂停", $"pause_{alert.Id}"),
                    InlineKeyboardButton.WithCallbackData("恢复", $"resume_{alert.Id}")
                ]
            ]);

            await _botClient.SendMessage(
                chatId: _chatId,
                text: $"""
                <pre>⚠️ {alert.Symbol} 警报触发 {trend}
                条件: {alert.Condition}
                {changeText}: {priceChange:F3} ({priceChangePercent:F3}%)</pre>
                """,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }

    public async Task Handle(AlertStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        if (_activeAlerts.TryGetValue(notification.AlertId, out var alert))
        {
            alert.IsActive = notification.IsActive;
            _logger.LogInformation("Alert {Condition}, status updated to {Status}",
                alert.Condition,
                notification.IsActive ? "active" : "inactive");
        }
        await Task.CompletedTask;
    }
}
