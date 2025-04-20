using System.ComponentModel.DataAnnotations;
using MediatR;
using Trading.Application.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class CreateAlarmCommandHandler : IRequestHandler<CreateAlarmCommand, Alarm>
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly IMediator _mediator;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;

    public CreateAlarmCommandHandler(IAlarmRepository alarmRepository,
                                     JavaScriptEvaluator javaScriptEvaluator,
                                     IMediator mediator)
    {
        _mediator = mediator;
        _javaScriptEvaluator = javaScriptEvaluator;
        _alarmRepository = alarmRepository;
    }

    public async Task<Alarm> Handle(CreateAlarmCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Validate the command
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            var errorMessage = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            throw new ValidationException(errorMessage);
        }
        CommonHelper.ConvertToKlineInterval(request.Interval);
        // Validate JavaScript expression
        if (!_javaScriptEvaluator.ValidateExpression(request.Expression, out var message))
        {
            throw new ValidationException($"Invalid expression: {message}");
        }
        var alarm = new Alarm
        {
            Symbol = request.Symbol.ToUpper(),
            Interval = request.Interval,
            Expression = request.Expression,
            IsActive = true,
            LastNotification = DateTime.UtcNow,
        };
        await _alarmRepository.AddAsync(alarm, cancellationToken);
        await _mediator.Publish(new AlarmCreatedEvent(alarm), cancellationToken);
        return alarm;
    }
}
