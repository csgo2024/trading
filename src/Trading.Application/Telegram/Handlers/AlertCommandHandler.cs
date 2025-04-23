using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Commands;
using Trading.Common.Extensions;
using Trading.Common.Models;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class AlertCommandHandler : ICommandHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertCommandHandler> _logger;
    private readonly IMediator _mediator;
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;
    public static string Command => "/alert";
    public static string CallbackPrefix => "alert";

    public AlertCommandHandler(ILogger<AlertCommandHandler> logger,
                               IMediator mediator,
                               IAlertRepository alertRepository,
                               ITelegramBotClient botClient,
                               IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _mediator = mediator;
        _botClient = botClient;
        _chatId = settings.Value.ChatId!;
    }

    public async Task HandleAsync(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            await HandleDefault();
            return;
        }
        // 处理清空命令
        if (parameters.Trim().Equals("empty", StringComparison.OrdinalIgnoreCase))
        {
            await HandleEmpty();
            return;
        }

        var parts = parameters.Trim().Split([' '], 2);
        var subCommand = parts[0].ToLower();
        var subParameters = parts.Length > 1 ? parts[1] : string.Empty;

        switch (subCommand)
        {
            case "create":
                await HandleCreate(subParameters);
                break;
            case "delete":
                await HandleDelete(subParameters);
                break;
            case "pause":
                await HandlPause(subParameters);
                break;
            case "resume":
                await HandleResume(subParameters);
                break;
            default:
                _logger.LogError("Unknown command. Use: create, delete, pause, or resume");
                break;
        }
    }

    private static (string emoji, string status) GetStatusInfo(Alert alert) => alert.Status switch
    {
        StateStatus.Running => ("🟢", "运行中"),
        StateStatus.Paused => ("🔴", "已暂停"),
        _ => ("⚠️", "未知状态")
    };

    private async Task HandleDefault()
    {
        var alerts = await _alertRepository.GetAllAlerts();
        if (alerts.Count == 0)
        {
            _logger.LogInformation("Alert is empty, please create and call later.");
            return;
        }
        foreach (var alert in alerts)
        {
            var (emoji, status) = GetStatusInfo(alert);
            var safeExpression = alert.Expression.ToTelegramSafeString();
            var text = $"""
            ⏰ <b>警报状态</b> ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})
            <pre>{emoji} [{alert.Symbol}-{alert.Interval}]:{status}
            表达式：{safeExpression}</pre>
            """;
            var buttons = alert.Status switch
            {
                StateStatus.Running => [InlineKeyboardButton.WithCallbackData("⏸️ 暂停", $"alert_pause_{alert.Id}")],
                StateStatus.Paused => new[] { InlineKeyboardButton.WithCallbackData("▶️ 启用", $"alert_resume_{alert.Id}") },
                _ => throw new InvalidOperationException()
            };
            buttons = [.. buttons, InlineKeyboardButton.WithCallbackData("🗑️ 删除", $"alert_delete_{alert.Id}")];
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
    private async Task HandleEmpty()
    {
        var count = await _alertRepository.ClearAllAlertsAsync(CancellationToken.None);
        await _mediator.Publish(new AlertEmptyedEvent());
        _logger.LogInformation("已清空所有价格警报，共删除 {Count} 个警报", count);
        return;
    }
    private async Task HandleCreate(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json, nameof(json));
        var command = JsonConvert.DeserializeObject<CreateAlertCommand>(json) ?? throw new InvalidOperationException("Failed to parse alert parameters");
        await _mediator.Send(command);
    }

    private async Task HandleDelete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var command = new DeleteAlertCommand { Id = id.Trim() };
        var result = await _mediator.Send(command);
        if (!result)
        {
            throw new InvalidOperationException($"Failed to delete alert {id}");
        }
        _logger.LogInformation("Alert {id} deleted successfully.", id);
    }

    private async Task HandlPause(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            _logger.LogError("未找到报警 ID: {AlertId}", id);
            return;
        }
        alert.Status = StateStatus.Paused;
        alert.UpdatedAt = DateTime.UtcNow;
        await _alertRepository.UpdateAsync(id, alert);
        await _mediator.Publish(new AlertPausedEvent(id));
        _logger.LogInformation("Alert {id} paused successfully.", id);
    }

    private async Task HandleResume(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            _logger.LogError("未找到报警 ID: {AlertId}", id);
            return;
        }
        alert.Status = StateStatus.Running;
        alert.UpdatedAt = DateTime.UtcNow;
        await _alertRepository.UpdateAsync(id, alert);
        await _mediator.Publish(new AlertResumedEvent(alert));
        _logger.LogInformation("Alert {id} resumed successfully.", id);
    }

    public async Task HandleCallbackAsync(string action, string parameters)
    {
        var alertId = parameters.Trim();
        switch (action)
        {
            case "pause":
                await HandlPause(alertId);
                break;

            case "resume":
                await HandleResume(alertId);
                break;

            case "delete":
                await HandleDelete(alertId);
                break;
        }
    }
}
