using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Trading.Application.Commands;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class AlarmCommandHandler : ICommandHandler
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly ILogger<AlarmCommandHandler> _logger;
    private readonly IMediator _mediator;
    public static string Command => "/alarm";
    public static string CallbackPrefix => "alarm";

    public AlarmCommandHandler(ILogger<AlarmCommandHandler> logger,
                               IMediator mediator,
                               IAlarmRepository alarmRepository)
    {
        _logger = logger;
        _alarmRepository = alarmRepository;
        _mediator = mediator;
    }

    public async Task HandleAsync(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            _logger.LogError("<pre>Invalid command format. Use: /alarm [create|delete|pause|resume|empty] [parameters]</pre>");
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
                _logger.LogError("<pre>Unknown command. Use: create, delete, pause, or resume</pre>");
                break;
        }
    }

    private async Task HandleEmpty()
    {
        var count = await _alarmRepository.ClearAllAlarmsAsync(CancellationToken.None);
        await _mediator.Publish(new AlarmEmptyedEvent());
        _logger.LogInformation("<pre>已清空所有价格警报，共删除 {Count} 个警报</pre>", count);
        return;
    }
    private async Task HandleCreate(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json, nameof(json));
        var command = JsonConvert.DeserializeObject<CreateAlarmCommand>(json) ?? throw new InvalidOperationException("Failed to parse alarm parameters");
        await _mediator.Send(command);
    }

    private async Task HandleDelete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var command = new DeleteAlarmCommand { Id = id.Trim() };
        var result = await _mediator.Send(command);
        if (!result)
        {
            throw new InvalidOperationException($"Failed to delete alarm {id}");
        }
        _logger.LogInformation("Alarm {id} deleted successfully.", id);
    }

    private async Task HandlPause(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var alarm = await _alarmRepository.GetByIdAsync(id);
        if (alarm == null)
        {
            _logger.LogError("<pre>未找到报警 ID: {AlarmId}</pre>", id);
            return;
        }
        alarm.IsActive = false;
        alarm.UpdatedAt = DateTime.UtcNow;
        await _alarmRepository.UpdateAsync(id, alarm);
        await _mediator.Publish(new AlarmPausedEvent(id));
    }

    private async Task HandleResume(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var alarm = await _alarmRepository.GetByIdAsync(id);
        if (alarm == null)
        {
            _logger.LogError("<pre>未找到报警 ID: {AlarmId}</pre>", id);
            return;
        }
        alarm.IsActive = true;
        alarm.UpdatedAt = DateTime.UtcNow;
        await _alarmRepository.UpdateAsync(id, alarm);
        await _mediator.Publish(new AlarmResumedEvent(alarm));
    }

    public async Task HandleCallbackAsync(string action, string parameters)
    {
        var alarmId = parameters.Trim();
        switch (action)
        {
            case "pause":
                await HandlPause(alarmId);
                break;

            case "resume":
                await HandleResume(alarmId);
                break;

            case "delete":
                await HandleDelete(alarmId);
                break;
        }
    }
}
