using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Common.Tools;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class PriceAlertCommandHandler : ICommandHandler
{
    private readonly ILogger<PriceAlertCommandHandler> _logger;
    private readonly IPriceAlertRepository _alertRepository;
    private readonly IMediator _mediator;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    public static string Command => "/alert";

    public PriceAlertCommandHandler(
        ILogger<PriceAlertCommandHandler> logger,
        IMediator mediator,
        JavaScriptEvaluator javaScriptEvaluator,
        IPriceAlertRepository alertRepository)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _mediator = mediator;
        _javaScriptEvaluator = javaScriptEvaluator;
    }

    public async Task HandleAsync(string parameters)
    {
        try
        {
            var parts = parameters.Trim().Split([' '], 2);
            if (parts.Length != 2)
            {
                _logger.LogError("<pre>格式错误! 正确格式:\n/alert BTCUSDT close > 50000</pre>");
                return;
            }

            var symbol = parts[0].ToUpper();
            var condition = parts[1];

            // Validate JavaScript condition
            if (!_javaScriptEvaluator.ValidateCondition(condition, out var message))
            {
                _logger.LogError("<pre>条件语法错误: {Message}</pre>", message);
                return;
            }

            var alert = new PriceAlert
            {
                Symbol = symbol,
                Condition = condition,
                IsActive = true,
                LastNotification = DateTime.UtcNow,
            };

            await _alertRepository.AddAsync(alert);
            await _mediator.Publish(new PriceAlertCreatedEvent());
            _logger.LogInformation("<pre>已设置 {Symbol} 价格预警\n条件: {Condition}</pre>", symbol, condition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create alert failed");
        }
    }

    public async Task HandleCallbackAsync(string callbackData)
    {
        try
        {
            var parts = callbackData.Split('_');
            var action = parts[0];
            var alertId = parts[1];
            var alert = await _alertRepository.GetByIdAsync(alertId);
            switch (action)
            {
                case "pause":
                    alert.IsActive = false;
                    alert.UpdatedAt = DateTime.UtcNow;
                    await _alertRepository.UpdateAsync(alertId, alert);
                    await _mediator.Publish(new AlertStatusChangedEvent(alertId, false));
                    break;

                case "resume":
                    alert.IsActive = true;
                    alert.UpdatedAt = DateTime.UtcNow;
                    await _alertRepository.UpdateAsync(alertId, alert);
                    await _mediator.Publish(new AlertStatusChangedEvent(alertId, true));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理价格报警回调失败");
        }
    }
}
