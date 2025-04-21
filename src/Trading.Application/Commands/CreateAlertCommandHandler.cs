using System.ComponentModel.DataAnnotations;
using MediatR;
using Trading.Application.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class CreateAlertCommandHandler : IRequestHandler<CreateAlertCommand, Alert>
{
    private readonly IAlertRepository _alertRepository;
    private readonly IMediator _mediator;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;

    public CreateAlertCommandHandler(IAlertRepository alertRepository,
                                     JavaScriptEvaluator javaScriptEvaluator,
                                     IMediator mediator)
    {
        _mediator = mediator;
        _javaScriptEvaluator = javaScriptEvaluator;
        _alertRepository = alertRepository;
    }

    public async Task<Alert> Handle(CreateAlertCommand request, CancellationToken cancellationToken)
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
        var alert = new Alert
        {
            Symbol = request.Symbol.ToUpper(),
            Interval = request.Interval,
            Expression = request.Expression,
            Status = StateStatus.Running,
            LastNotification = DateTime.UtcNow,
        };
        await _alertRepository.AddAsync(alert, cancellationToken);
        await _mediator.Publish(new AlertCreatedEvent(alert), cancellationToken);
        return alert;
    }
}
