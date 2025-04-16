using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class AlarmCommandHandler : ICommandHandler
{
    private readonly ILogger<AlarmCommandHandler> _logger;
    private readonly IAlarmRepository _alarmRepository;
    private readonly IMediator _mediator;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    public static string Command => "/alarm";

    public AlarmCommandHandler(
        ILogger<AlarmCommandHandler> logger,
        IMediator mediator,
        JavaScriptEvaluator javaScriptEvaluator,
        IAlarmRepository alarmRepository)
    {
        _logger = logger;
        _alarmRepository = alarmRepository;
        _mediator = mediator;
        _javaScriptEvaluator = javaScriptEvaluator;
    }

    public async Task HandleAsync(string parameters)
    {
        try
        {
            // 处理清空命令
            if (parameters.Trim().Equals("empty", StringComparison.OrdinalIgnoreCase))
            {
                var count = await _alarmRepository.ClearAllAlarmsAsync(CancellationToken.None);
                await _mediator.Publish(new AlarmEmptyEvent());
                _logger.LogInformation("<pre>已清空所有价格警报，共删除 {Count} 个警报</pre>", count);
                return;
            }
            var parts = parameters.Trim().Split([' '], 3);
            if (parts.Length != 3)
            {
                _logger.LogError("<pre>格式错误! 正确格式:\n/alarm BTCUSDT 1h close > 50000</pre>");
                return;
            }

            var symbol = parts[0].ToUpper();
            var interval = parts[1];
            var condition = parts[2];

            // Validate JavaScript condition
            if (!_javaScriptEvaluator.ValidateCondition(condition, out var message))
            {
                _logger.LogError("<pre>条件语法错误: {Message}</pre>", message);
                return;
            }

            var alarm = new Alarm
            {
                Symbol = symbol,
                Interval = interval,
                Condition = condition,
                IsActive = true,
                LastNotification = DateTime.UtcNow,
            };

            await _alarmRepository.AddAsync(alarm);
            await _mediator.Publish(new AlarmCreatedEvent(alarm));
            _logger.LogInformation("<pre>已设置 {Symbol} 价格警报\n表达式: {Condition}</pre>", symbol, condition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create alarm failed");
        }
    }

    public async Task HandleCallbackAsync(string callbackData)
    {
        try
        {
            var parts = callbackData.Split('_');
            var action = parts[0];
            var alarmId = parts[1];
            var alarm = await _alarmRepository.GetByIdAsync(alarmId);
            if (alarm == null)
            {
                _logger.LogError("<pre>未找到报警 ID: {AlarmId}</pre>", alarmId);
                return;
            }
            switch (action)
            {
                case "pause":
                    alarm.IsActive = false;
                    alarm.UpdatedAt = DateTime.UtcNow;
                    await _alarmRepository.UpdateAsync(alarmId, alarm);
                    await _mediator.Publish(new AlarmPausedEvent(alarmId));
                    break;

                case "resume":
                    alarm.IsActive = true;
                    alarm.UpdatedAt = DateTime.UtcNow;
                    await _alarmRepository.UpdateAsync(alarmId, alarm);
                    await _mediator.Publish(new AlarmResumedEvent(alarm));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理价格报警回调失败");
        }
    }
}
