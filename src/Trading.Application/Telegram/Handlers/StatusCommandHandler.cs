using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Services.Alarms;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class StatusCommandHandler : ICommandHandler
{
    private readonly AlarmNotificationService _alarmNotificationService;
    private readonly ILogger<StatusCommandHandler> _logger;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IAlarmRepository _alarmRepository;

    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    public static string Command => "/status";

    public StatusCommandHandler(
        IStrategyRepository strategyRepository,
        IAlarmRepository alarmRepository,
        AlarmNotificationService alarmNotificationService,
        ITelegramBotClient botClient,
        IOptions<TelegramSettings> settings,
        ILogger<StatusCommandHandler> logger)
    {
        _strategyRepository = strategyRepository;
        _alarmRepository = alarmRepository;
        _alarmNotificationService = alarmNotificationService;
        _logger = logger;
        _botClient = botClient;
        _chatId = settings.Value.ChatId!;
    }

    public async Task HandleAsync(string parameters)
    {
        var strategies = await _strategyRepository.GetAllStrategies();
        var alarms = await _alarmRepository.GetAllAlerts();

        foreach (var strategy in strategies)
        {
            var (emoji, status) = GetStatusInfo(strategy);
            var text = $"""
            📊 <b>策略状态报告</b> ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})
            ------------------------
            • {emoji} [{strategy.AccountType}-{strategy.Symbol}]: {status}
            • 跌幅: {strategy.PriceDropPercentage} / 目标价格: {strategy.TargetPrice} 💰
            • 金额: {strategy.Amount} / 数量: {strategy.Quantity}
            ------------------------
            """;
            var buttons = strategy.Status switch
            {
                StateStatus.Running => new[] { InlineKeyboardButton.WithCallbackData("⏸️ 暂停", $"strategy_pause_{strategy.Id}") },
                StateStatus.Paused => new[] { InlineKeyboardButton.WithCallbackData("▶️ 启用", $"strategy_resume_{strategy.Id}") },
                _ => throw new InvalidOperationException()
            };
            buttons = [.. buttons, InlineKeyboardButton.WithCallbackData("🗑️ 删除", $"strategy_delete_{strategy.Id}")];
            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = text,
                ParseMode = ParseMode.Html,
                DisableNotification = true,
                ReplyMarkup = new InlineKeyboardMarkup([buttons])
            }, CancellationToken.None);
            Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None).Wait();
            _logger.LogDebug(text);
        }
        foreach (var alarm in alarms)
        {
            var status = alarm.IsActive ? "🟢 运行中" : "🔴 已暂停";
            var safeExpression = alarm.Expression.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var text = $"""
            ⏰ <b>警报</b> ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})
            ------------------------
            {status}
            <blockquote>{safeExpression}</blockquote>
            <blockquote>{alarm.Symbol} - {alarm.Interval}</blockquote>
            ------------------------
            """;
            var buttons = alarm.IsActive switch
            {
                true => new[] { InlineKeyboardButton.WithCallbackData("⏸️ 暂停", $"alarm_pause_{alarm.Id}") },
                false => new[] { InlineKeyboardButton.WithCallbackData("▶️ 启用", $"alarm_resume_{alarm.Id}") }
            };
            buttons = [.. buttons, InlineKeyboardButton.WithCallbackData("🗑️ 删除", $"alarm_delete_{alarm.Id}")];
            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = text,
                ParseMode = ParseMode.Html,
                DisableNotification = true,
                ReplyMarkup = new InlineKeyboardMarkup([buttons])
            }, CancellationToken.None);
            _logger.LogDebug(text);
        }
    }

    private static (string emoji, string status) GetStatusInfo(Strategy strategy) => strategy.Status switch
    {
        StateStatus.Running => ("🟢", "运行中"),
        StateStatus.Paused => ("🔴", "已暂停"),
        _ => ("⚠️", "未知状态")
    };

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
