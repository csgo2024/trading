using System.Collections.Concurrent;
using Binance.Net.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Common.Models;
using Trading.Common.Tools;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.API.Services.Alerts;

public class PriceAlertManager : INotificationHandler<AlertStatusChangedEvent>,
    INotificationHandler<PriceAlertCreatedEvent>
{
    private readonly ILogger<PriceAlertManager> _logger;
    private readonly IPriceAlertRepository _alertRepository;
    private readonly ITelegramBotClient _botClient;
    private readonly KlineStreamManager _streamManager;
    private readonly string _chatId;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private static readonly ConcurrentDictionary<string, PriceAlert> _activeAlerts = new();
    private readonly ConcurrentDictionary<string, IBinanceKline> _lastkLines = new();
    private readonly ConcurrentDictionary<string, (CancellationTokenSource cts, Task task)> _alertTasks = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _taskLock = new(1, 1);

    private readonly HashSet<string> _subcribedSymbols = new();

    public PriceAlertManager(
        ILogger<PriceAlertManager> logger,
        IPriceAlertRepository alertRepository,
        ITelegramBotClient botClient,
        JavaScriptEvaluator javaScriptEvaluator,
        KlineStreamManager klineStreamManager,
        IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _javaScriptEvaluator = javaScriptEvaluator;
        _streamManager = klineStreamManager;
        _streamManager.OnKlineUpdate += HandleKlineUpdate;
    }
    #region Private Methods

    private void HandleKlineUpdate(string symbol, IBinanceKline kline)
    {
        _lastkLines.AddOrUpdate(symbol, kline, (_, _) => kline);
        var alerts = _activeAlerts.Values.Where(x => x.Symbol == symbol && x.IsActive);

        foreach (var alert in alerts)
        {
            // 为每个alert创建独立的检查任务
            if (!_alertTasks.ContainsKey(alert.Id))
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                var task = RunAlertCheckLoop(alert.Id, cts.Token);
                _alertTasks.TryAdd(alert.Id, (cts, task));
            }
        }
    }

    private async Task RunAlertCheckLoop(string alertId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_activeAlerts.TryGetValue(alertId, out var alert) || !alert.IsActive)
                {
                    break;
                }

                if (_lastkLines.TryGetValue(alert.Symbol, out var kline))
                {
                    if ((DateTime.UtcNow - alert.LastNotification).TotalSeconds >= 30 &&
                        _javaScriptEvaluator.EvaluateCondition(
                            alert.Condition,
                            kline.OpenPrice,
                            kline.ClosePrice,
                            kline.HighPrice,
                            kline.LowPrice))
                    {
                        SendAlert(alert, kline);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alert {AlertId}", alertId);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        // 任务结束时清理资源
        await _taskLock.WaitAsync(ct);
        try
        {
            if (_alertTasks.TryRemove(alertId, out var taskInfo))
            {
                taskInfo.cts.Dispose();
            }
        }
        finally
        {
            _taskLock.Release();
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
                ⏰ {DateTime.UtcNow.AddHours(8)}
                <pre>⚠️ {alert.Symbol} 警报触发 {trend}
                条件: {alert.Condition}
                收盘价格: {kline.ClosePrice}
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
    #endregion

    #region Public Methods
    public async Task LoadPriceAlertsAsync(CancellationToken ct)
    {
        var alerts = await _alertRepository.GetActiveAlertsAsync(ct);
        var priceAlerts = alerts.ToList();
        var symbols = priceAlerts.Select(a => a.Symbol).Distinct().ToHashSet();

        var needReconnect = _streamManager.NeedsReconnection();
        var symbolChanged = !_subcribedSymbols.SetEquals(symbols);

        // 如果需要重连或币种发生变化，重新订阅
        if (needReconnect || symbolChanged)
        {
            _subcribedSymbols.Clear();
            await _streamManager.SubscribeSymbols(symbols, ct);
            _subcribedSymbols.UnionWith(symbols);
        }
        // 更新内存中的警告
        foreach (var alert in priceAlerts)
        {
            _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
        }
    }
    public async Task StopAsync()
    {
        await _cts.CancelAsync();
        await _taskLock.WaitAsync();
        try
        {
            foreach (var (cts, task) in _alertTasks.Values)
            {
                await cts.CancelAsync();
                await task;
                cts.Dispose();
            }
            _alertTasks.Clear();
        }
        finally
        {
            _taskLock.Release();
        }
    }

    private async Task StopAlertAsync(string alertId)
    {
        await _taskLock.WaitAsync();
        try
        {
            if (_alertTasks.TryRemove(alertId, out var taskInfo))
            {
                await taskInfo.cts.CancelAsync();
                await taskInfo.task;
                taskInfo.cts.Dispose();
            }
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public Task Handle(AlertStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        if (_activeAlerts.TryGetValue(notification.AlertId, out var alert))
        {
            alert.IsActive = notification.IsActive;

            if (notification.IsActive)
            {
                // 重新加载该告警, 并重新订阅webSocket stream流
                _ = LoadPriceAlertsAsync(cancellationToken);
            }
            else
            {
                // 停止该告警的检查任务
                _ = StopAlertAsync(notification.AlertId);
            }

            _logger.LogInformation("{Symbol} Alert {Condition} status updated to {Status}",
                alert.Symbol,
                alert.Condition,
                notification.IsActive ? "active" : "inactive");
        }
        return Task.CompletedTask;
    }

    public async Task Handle(PriceAlertCreatedEvent notification, CancellationToken cancellationToken)
    {
        await LoadPriceAlertsAsync(cancellationToken);
    }
    #endregion
}
