using System.Collections.Concurrent;
using Binance.Net.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Services.Common;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Alerts;

public class AlertNotificationService :
    INotificationHandler<KlineClosedEvent>,
    INotificationHandler<AlertCreatedEvent>,
    INotificationHandler<AlertPausedEvent>,
    INotificationHandler<AlertResumedEvent>,
    INotificationHandler<AlertDeletedEvent>,
    INotificationHandler<AlertEmptyedEvent>
{
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertNotificationService> _logger;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private static readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();
    private static readonly ConcurrentDictionary<string, IBinanceKline> _lastkLines = new();
    public AlertNotificationService(ILogger<AlertNotificationService> logger,
                                    IAlertRepository alertRepository,
                                    JavaScriptEvaluator javaScriptEvaluator,
                                    IBackgroundTaskManager backgroundTaskManager
                                    )
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _javaScriptEvaluator = javaScriptEvaluator;
        _backgroundTaskManager = backgroundTaskManager;
    }

    public async Task Handle(KlineClosedEvent notification, CancellationToken cancellationToken)
    {
        var kline = notification.Kline;
        var key = $"{notification.Symbol}-{notification.Interval}";
        _lastkLines.AddOrUpdate(key, kline, (_, _) => kline);
        _logger.LogDebug("LastkLines: {@LastKlines} after klineUpdate.", _lastkLines);

        // reset paused alerts to running if the kline is closed
        var idsToUpdate = await _alertRepository.ResumeAlertAsync(notification.Symbol,
                                                                  BinanceHelper.ConvertToIntervalString(notification.Interval),
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
        await _backgroundTaskManager.StartAsync(TaskCategory.Alert,
                                                alert.Id,
                                                ct => ProcessAlert(alert, ct),
                                                cancellationToken);
    }

    public async Task Handle(AlertPausedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlerts.TryRemove(notification.Alert.Id, out _);
        await _backgroundTaskManager.StopAsync(TaskCategory.Alert, notification.Alert.Id);
    }

    public async Task Handle(AlertResumedEvent notification, CancellationToken cancellationToken)
    {
        var alert = notification.Alert;
        _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
        await _backgroundTaskManager.StartAsync(TaskCategory.Alert,
                                                alert.Id,
                                                ct => ProcessAlert(alert, ct),
                                                cancellationToken);
    }
    public async Task Handle(AlertDeletedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlerts.TryRemove(notification.Alert.Id, out _);
        await _backgroundTaskManager.StopAsync(TaskCategory.Alert, notification.Alert.Id);
    }
    public async Task Handle(AlertEmptyedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlerts.Clear();
        _lastkLines.Clear();
        _logger.LogInformation("Alert list is empty, stopping all monitors.");
        // Stop all monitors
        await _backgroundTaskManager.StopAsync(TaskCategory.Alert);
    }

    public async Task InitWithAlerts(IEnumerable<Alert> alerts, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var alert in alerts)
            {
                if (!_activeAlerts.ContainsKey(alert.Id))
                {
                    _activeAlerts.AddOrUpdate(alert.Id, alert, (_, _) => alert);
                    await _backgroundTaskManager.StartAsync(TaskCategory.Alert,
                                                            alert.Id,
                                                            ct => ProcessAlert(alert, ct),
                                                            cancellationToken);
                }
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
        var key = $"{alert.Symbol}-{BinanceHelper.ConvertToKlineInterval(alert.Interval)}";
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
                        SendNotification(alert, kline);
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
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alert {AlertId}", alert.Id);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private void SendNotification(Alert alert, IBinanceKline kline)
    {
        try
        {

            var priceChange = kline.ClosePrice - kline.OpenPrice;
            var priceChangePercent = priceChange / kline.OpenPrice * 100;
            var changeText = priceChange >= 0 ? "🟢 " : "🔴 ";

            var text = $"""
            Expression: {alert.Expression}
            Open: {kline.OpenPrice} Close: {kline.ClosePrice}
            High: {kline.HighPrice} Low: {kline.LowPrice}
            {changeText}: {priceChange:F3} ({priceChangePercent:F3}%)
            """;

            var telegramScope = new TelegramLoggerScope
            {
                Title = $"⏰ Alarm Triggered: {alert.Symbol}-{alert.Interval}",
                DisableNotification = false,
                ReplyMarkup = new InlineKeyboardMarkup(
                [
                    [
                        InlineKeyboardButton.WithCallbackData("Pause", $"alert_pause_{alert.Id}")
                    ]
                ])
            };
            using (_logger.BeginScope(telegramScope))
            {
                _logger.LogInformation(text);
            }

            alert.LastNotification = DateTime.UtcNow;
            alert.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }
}
