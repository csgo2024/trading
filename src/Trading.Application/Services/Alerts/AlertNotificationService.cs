using System.Collections.Concurrent;
using Binance.Net.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Helpers;
using Trading.Application.Services.Common;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Alerts;

public class AlertNotificationService :
    INotificationHandler<KlineUpdateEvent>,
    INotificationHandler<AlertCreatedEvent>,
    INotificationHandler<AlertPausedEvent>,
    INotificationHandler<AlertResumedEvent>,
    INotificationHandler<AlertDeletedEvent>,
    INotificationHandler<AlertEmptyedEvent>
{
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertNotificationService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private readonly string _chatId;
    private static readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();
    private static readonly ConcurrentDictionary<string, IBinanceKline> _lastkLines = new();
    public AlertNotificationService(ILogger<AlertNotificationService> logger,
                                    IAlertRepository alertRepository,
                                    ITelegramBotClient botClient,
                                    JavaScriptEvaluator javaScriptEvaluator,
                                    IBackgroundTaskManager backgroundTaskManager,
                                    IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _javaScriptEvaluator = javaScriptEvaluator;
        _backgroundTaskManager = backgroundTaskManager;
    }

    public async Task Handle(KlineUpdateEvent notification, CancellationToken cancellationToken)
    {
        var kline = notification.Kline;
        var key = $"{notification.Symbol}-{notification.Interval}";
        _lastkLines.AddOrUpdate(key, kline, (_, _) => kline);
        _logger.LogDebug("LastkLines: {@LastKlines} after klineUpdate.", _lastkLines);
        // reset paused alerts to running if the kline is closed

        var idsToUpdate = await _alertRepository.ResumeAlertAsync(notification.Symbol,
                                                CommonHelper.ConvertToIntervalString(notification.Interval),
                                                cancellationToken);
        if (idsToUpdate.Count > 0)
        {
            var alerts = await _alertRepository.GetActiveAlertsAsync(cancellationToken);
            await InitWithAlerts(alerts, cancellationToken);
        }
        else
        {
            _logger.LogDebug("No alerts to resume for symbol {Symbol}", notification.Symbol);
        }
    }

    public async Task Handle(AlertCreatedEvent notification, CancellationToken cancellationToken)
    {
        var alert = notification.Alert;
        _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
        await _backgroundTaskManager.StartAsync(TaskCategories.Alert,
                                                alert.Id,
                                                ct => ProcessAlert(alert, ct),
                                                cancellationToken);
    }

    public async Task Handle(AlertPausedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlerts.TryRemove(notification.AlertId, out _);
        await _backgroundTaskManager.StopAsync(TaskCategories.Alert, notification.AlertId);
    }

    public async Task Handle(AlertResumedEvent notification, CancellationToken cancellationToken)
    {
        var alert = notification.Alert;
        _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
        await _backgroundTaskManager.StartAsync(TaskCategories.Alert,
                                                alert.Id,
                                                ct => ProcessAlert(alert, ct),
                                                cancellationToken);
    }
    public async Task Handle(AlertDeletedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlerts.TryRemove(notification.AlertId, out _);
        await _backgroundTaskManager.StopAsync(TaskCategories.Alert, notification.AlertId);
    }
    public async Task Handle(AlertEmptyedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlerts.Clear();
        _lastkLines.Clear();
        _logger.LogInformation("Alert list is empty, stopping all monitors.");
        // Stop all monitors
        await _backgroundTaskManager.StopAsync(TaskCategories.Alert);
    }

    public async Task InitWithAlerts(IEnumerable<Alert> alerts, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var alert in alerts)
            {
                _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
                await _backgroundTaskManager.StartAsync(TaskCategories.Alert,
                                                        alert.Id,
                                                        ct => ProcessAlert(alert, ct),
                                                        cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load active alerts");
        }
    }
    public async Task ProcessAlert(Alert alert, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting monitoring for alert {AlertId} ({Symbol}-{Interval}, Expression: {Expression})",
                         alert.Id,
                         alert.Symbol,
                         alert.Interval,
                         alert.Expression);
        var key = $"{alert.Symbol}-{CommonHelper.ConvertToKlineInterval(alert.Interval)}";
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_lastkLines.TryGetValue(key, out var kline))
                {
                    if ((DateTime.UtcNow - alert.LastNotification).TotalSeconds >= 60 &&
                        _javaScriptEvaluator.EvaluateExpression(
                            alert.Expression,
                            kline.OpenPrice,
                            kline.ClosePrice,
                            kline.HighPrice,
                            kline.LowPrice))
                    {
                        await SendNotification(alert, kline);
                    }
                }
                else
                {
                    // _logger.LogWarning("No kline data for symbol {Symbol}", alert.Symbol);
                }

                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alert {AlertId}", alert.Id);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task SendNotification(Alert alert, IBinanceKline kline)
    {
        try
        {

            var priceChange = kline.ClosePrice - kline.OpenPrice;
            var priceChangePercent = priceChange / kline.OpenPrice * 100;
            var changeText = priceChange >= 0 ? "🟢 上涨" : "🔴 下跌";

            var text = $"""
            ⏰ <b>️ {alert.Symbol}-{alert.Interval} 警报触发</b> ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})
            <pre>条件: {alert.Expression}
            开盘价格: {kline.OpenPrice} 收盘价格: {kline.ClosePrice}
            最高价格: {kline.HighPrice} 最低价格: {kline.LowPrice}
            {changeText}: {priceChange:F3} ({priceChangePercent:F3}%)</pre>
            """;
            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = text,
                ParseMode = ParseMode.Html,
                ReplyMarkup = new InlineKeyboardMarkup(
                [
                    [
                        InlineKeyboardButton.WithCallbackData("暂停", $"alert_pause_{alert.Id}")
                    ]
                ])
            }, CancellationToken.None);
            _logger.LogDebug(text);
            alert.LastNotification = DateTime.UtcNow;
            alert.UpdatedAt = DateTime.UtcNow;
            await _alertRepository.UpdateAsync(alert.Id, alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }
}
