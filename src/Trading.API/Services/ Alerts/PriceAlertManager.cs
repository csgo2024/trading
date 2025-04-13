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

public class PriceAlertManager : INotificationHandler<AlertStatusChangedEvent>
{
    private readonly ILogger<PriceAlertManager> _logger;
    private readonly IPriceAlertRepository _alertRepository;
    private readonly ITelegramBotClient _botClient;
    private readonly KlineStreamManager _streamManager;
    private readonly string _chatId;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private static readonly ConcurrentDictionary<string, PriceAlert> _activeAlerts = new();
    private readonly ConcurrentDictionary<string, IBinanceKline> _lastKlines = new();
    private readonly ConcurrentDictionary<string, Task> _alertTasks = new();
    private readonly CancellationTokenSource _cts = new();

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

    private async Task RefreshAlerts(CancellationToken ct)
    {
        var alerts = await _alertRepository.GetActiveAlertsAsync(ct);
        var symbols = alerts.Select(a => a.Symbol).Distinct().ToHashSet();

        // 更新内存中的警告
        foreach (var alert in alerts)
        {
            _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
        }

        // 如果需要重连或币种发生变化，重新订阅
        if (_streamManager.NeedsReconnection() || SymbolsChanged(symbols))
        {
            await _streamManager.SubscribeSymbols(symbols, ct);
        }
    }
    private static bool SymbolsChanged(HashSet<string> newSymbols)
    {
        var currentSymbols = _activeAlerts.Values.Select(x => x.Symbol).Distinct().ToHashSet();
        return !currentSymbols.SetEquals(newSymbols);
    }

    private void HandleKlineUpdate(string symbol, IBinanceKline kline)
    {
        _lastKlines.AddOrUpdate(symbol, kline, (_, _) => kline);

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
                        .Where(alert => _javaScriptEvaluator.EvaluateCondition(
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
    #endregion

    #region Public Methods
    public async Task LoadAlertsAsync(CancellationToken stoppingToken)
    {
        await RefreshAlerts(stoppingToken);
    }
    public async Task StopAsync()
    {
        _cts.Cancel();
        await Task.WhenAll(_alertTasks.Values);
    }

    public Task Handle(AlertStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        if (_activeAlerts.TryGetValue(notification.AlertId, out var alert))
        {
            alert.IsActive = notification.IsActive;
            _logger.LogInformation("Alert {AlertId} status updated to {Status}",
                notification.AlertId,
                notification.IsActive ? "active" : "inactive");
        }
        return Task.CompletedTask;
    }
    #endregion
}
