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

namespace Trading.Application.Services.Alarms;

public class AlarmNotificationService :
    INotificationHandler<KlineUpdateEvent>,
    INotificationHandler<AlarmCreatedEvent>,
    INotificationHandler<AlarmPausedEvent>,
    INotificationHandler<AlarmResumedEvent>,
    INotificationHandler<AlarmEmptyEvent>
{
    private readonly IBackgroundTaskManager _backgroundTaskManager;
    private readonly IAlarmRepository _alarmRepository;
    private readonly ILogger<AlarmNotificationService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private readonly string _chatId;
    private static readonly ConcurrentDictionary<string, Alarm> _activeAlarms = new();
    private static readonly ConcurrentDictionary<string, IBinanceKline> _lastkLines = new();
    public AlarmNotificationService(
        ILogger<AlarmNotificationService> logger,
        IAlarmRepository alarmRepository,
        ITelegramBotClient botClient,
        JavaScriptEvaluator javaScriptEvaluator,
        IBackgroundTaskManager backgroundTaskManager,
        IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _alarmRepository = alarmRepository;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _javaScriptEvaluator = javaScriptEvaluator;
        _backgroundTaskManager = backgroundTaskManager;
    }

    public Task Handle(KlineUpdateEvent notification, CancellationToken cancellationToken)
    {
        var kline = notification.Kline;
        var key = $"{notification.Symbol}-{notification.Interval}";
        _lastkLines.AddOrUpdate(key, kline, (_, _) => kline);
        _logger.LogDebug("LastkLines: {@LastKlines} after klineUpdate.", _lastkLines);
        return Task.CompletedTask;
    }

    public async Task Handle(AlarmCreatedEvent notification, CancellationToken cancellationToken)
    {
        var alarm = notification.Alarm;
        _activeAlarms.AddOrUpdate(alarm.Id, alarm, (_, _) => alarm);
        await _backgroundTaskManager.StartAsync(TaskCategories.Alarm,
                                                alarm.Id,
                                                ct => ProcessAlarm(alarm, ct),
                                                cancellationToken);
    }

    public async Task Handle(AlarmPausedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlarms.TryRemove(notification.AlarmId, out _);
        await _backgroundTaskManager.StopAsync(TaskCategories.Alarm, notification.AlarmId);
    }

    public async Task Handle(AlarmResumedEvent notification, CancellationToken cancellationToken)
    {
        var alarm = notification.Alarm;
        _activeAlarms.AddOrUpdate(alarm.Id, alarm, (_, _) => alarm);
        await _backgroundTaskManager.StartAsync(TaskCategories.Alarm,
                                                alarm.Id,
                                                ct => ProcessAlarm(alarm, ct),
                                                cancellationToken);
    }
    public async Task Handle(AlarmEmptyEvent notification, CancellationToken cancellationToken)
    {
        _activeAlarms.Clear();
        _lastkLines.Clear();
        _logger.LogInformation("Alarm list is empty, stopping all monitors.");
        // Stop all monitors
        await _backgroundTaskManager.StopAsync(TaskCategories.Alarm);
    }

    public IEnumerable<Alarm> GetActiveAlarms()
    {
        var alarmIds = _backgroundTaskManager.GetActiveTaskIds(TaskCategories.Alarm);
        var result = _alarmRepository.GetAlarmsById(alarmIds);
        return result;
    }

    public async Task InitWithAlarms(IEnumerable<Alarm> alarms, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var alarm in alarms)
            {
                _activeAlarms.AddOrUpdate(alarm.Id, alarm, (_, _) => alarm);
                await _backgroundTaskManager.StartAsync(TaskCategories.Alarm,
                                                        alarm.Id,
                                                        ct => ProcessAlarm(alarm, ct),
                                                        cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load active alarms");
        }
    }
    public async Task ProcessAlarm(Alarm alarm, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting monitoring for alarm {AlarmId} ({Symbol}-{Interval}, Expression: {Expression})",
                         alarm.Id,
                         alarm.Symbol,
                         alarm.Interval,
                         alarm.Expression);
        var key = $"{alarm.Symbol}-{CommonHelper.ConvertToKlineInterval(alarm.Interval)}";
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_lastkLines.TryGetValue(key, out var kline))
                {
                    if ((DateTime.UtcNow - alarm.LastNotification).TotalSeconds >= 60 &&
                        _javaScriptEvaluator.EvaluateExpression(
                            alarm.Expression,
                            kline.OpenPrice,
                            kline.ClosePrice,
                            kline.HighPrice,
                            kline.LowPrice))
                    {
                        await SendNotification(alarm, kline);
                    }
                }
                else
                {
                    // _logger.LogWarning("No kline data for symbol {Symbol}", alarm.Symbol);
                }

                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alarm {AlarmId}", alarm.Id);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task SendNotification(Alarm alarm, IBinanceKline kline)
    {
        try
        {
            // 计算涨跌幅
            var priceChange = kline.ClosePrice - kline.OpenPrice;
            var priceChangePercent = priceChange / kline.OpenPrice * 100;
            var changeText = priceChange >= 0 ? "🟢 上涨" : "🔴 下跌";

            var text = $"""
                ⏰ {DateTime.UtcNow.AddHours(8)}
                <pre>⚠️ {alarm.Symbol}-{alarm.Interval} 警报触发
                条件: {alarm.Expression}
                收盘价格: {kline.ClosePrice}
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
                        InlineKeyboardButton.WithCallbackData("暂停", $"pause_{alarm.Id}"),
                        InlineKeyboardButton.WithCallbackData("恢复", $"resume_{alarm.Id}")
                    ]
                ])
            }, CancellationToken.None);
            _logger.LogDebug(text);
            alarm.LastNotification = DateTime.UtcNow;
            alarm.UpdatedAt = DateTime.UtcNow;
            await _alarmRepository.UpdateAsync(alarm.Id, alarm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alarm");
        }
    }
}
