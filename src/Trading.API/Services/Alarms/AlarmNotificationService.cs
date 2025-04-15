using System.Collections.Concurrent;
using Binance.Net.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Common.Models;
using Trading.Common.Tools;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.API.Services.Alarms;

public class AlarmNotificationService :
    INotificationHandler<KlineUpdateEvent>,
    INotificationHandler<AlarmCreatedEvent>,
    INotificationHandler<AlarmPausedEvent>,
    INotificationHandler<AlarmResumedEvent>
{
    private readonly ILogger<AlarmNotificationService> _logger;
    private readonly IAlarmRepository _alarmRepository;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private static readonly ConcurrentDictionary<string, IBinanceKline> _lastkLines = new();
    private readonly ConcurrentDictionary<string, Alarm> _activeAlarms = new();

    private readonly AlarmTaskManager _alarmTaskManager;

    public AlarmNotificationService(
        ILogger<AlarmNotificationService> logger,
        IAlarmRepository alarmRepository,
        ITelegramBotClient botClient,
        JavaScriptEvaluator javaScriptEvaluator,
        AlarmTaskManager alarmTaskManager,
        IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _alarmRepository = alarmRepository;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings));
        _javaScriptEvaluator = javaScriptEvaluator;
        _alarmTaskManager = alarmTaskManager;
    }

    public Task Handle(KlineUpdateEvent notification, CancellationToken cancellationToken)
    {
        var symbol = notification.Symbol;
        var kline = notification.Kline;
        _lastkLines.AddOrUpdate(symbol, kline, (_, _) => kline);
        _logger.LogDebug("LastkLines: {@LastKlines} after klineUpdate.", _lastkLines);
        return Task.CompletedTask;
    }

    public async Task Handle(AlarmCreatedEvent notification, CancellationToken cancellationToken)
    {
        var alarm = notification.Alarm;
        _activeAlarms.AddOrUpdate(alarm.Id, alarm, (_, _) => alarm);
        await _alarmTaskManager.StartMonitor(alarm.Id, ct => ProcessAlarm(alarm, ct), cancellationToken);
    }

    public async Task Handle(AlarmPausedEvent notification, CancellationToken cancellationToken)
    {
        _activeAlarms.TryRemove(notification.AlarmId, out _);
        await _alarmTaskManager.StopMonitor(notification.AlarmId);
    }

    public async Task Handle(AlarmResumedEvent notification, CancellationToken cancellationToken)
    {
        var alarm = notification.Alarm;
        _activeAlarms.AddOrUpdate(alarm.Id, alarm, (_, _) => alarm);
        await _alarmTaskManager.StartMonitor(alarm.Id, ct => ProcessAlarm(alarm, ct), cancellationToken);
    }

    public async Task InitWithAlarms(IEnumerable<Alarm> alarms, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var alarm in alarms)
            {
                _activeAlarms.AddOrUpdate(alarm.Id, alarm, (_, _) => alarm);
                await _alarmTaskManager.StartMonitor(alarm.Id, ct => ProcessAlarm(alarm, ct), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load active alarms");
        }
    }
    public async Task ProcessAlarm(Alarm alarm, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting monitoring for alarm {AlarmId} ({Symbol}, Condition: {Condition})",
                         alarm.Id,
                         alarm.Symbol,
                         alarm.Condition);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_lastkLines.TryGetValue(alarm.Symbol, out var kline))
                {
                    if ((DateTime.UtcNow - alarm.LastNotification).TotalSeconds >= 60 &&
                        _javaScriptEvaluator.EvaluateCondition(
                            alarm.Condition,
                            kline.OpenPrice,
                            kline.ClosePrice,
                            kline.HighPrice,
                            kline.LowPrice))
                    {
                        SendNotification(alarm, kline);
                    }
                }
                else
                {
                    _logger.LogWarning("No kline data for symbol {Symbol}", alarm.Symbol);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
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

    private async void SendNotification(Alarm alarm, IBinanceKline kline)
    {
        try
        {
            // 计算涨跌幅
            var priceChange = kline.ClosePrice - kline.OpenPrice;
            var priceChangePercent = priceChange / kline.OpenPrice * 100;
            var changeText = priceChange >= 0 ? "🟢 上涨" : "🔴 下跌";

            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = $"""
                ⏰ {DateTime.UtcNow.AddHours(8)}
                <pre>⚠️ {alarm.Symbol} 警报触发
                条件: {alarm.Condition}
                收盘价格: {kline.ClosePrice}
                {changeText}: {priceChange:F3} ({priceChangePercent:F3}%)</pre>
                """,
                ParseMode = ParseMode.Html,
                ReplyMarkup = new InlineKeyboardMarkup(
                [
                    [
                        InlineKeyboardButton.WithCallbackData("暂停", $"pause_{alarm.Id}"),
                        InlineKeyboardButton.WithCallbackData("恢复", $"resume_{alarm.Id}")
                    ]
                ])
            }, CancellationToken.None);

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
