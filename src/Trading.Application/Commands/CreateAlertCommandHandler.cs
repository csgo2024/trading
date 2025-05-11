using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MediatR;
using Trading.Application.JavaScript;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Commands;

public class CreateAlertCommandHandler : IRequestHandler<CreateAlertCommand, Alert>
{
    private readonly IAlertRepository _alertRepository;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;

    public CreateAlertCommandHandler(
        IAlertRepository alertRepository,
        JavaScriptEvaluator javaScriptEvaluator)
    {
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
        BinanceHelper.ConvertToKlineInterval(request.Interval);
        // Validate JavaScript expression
        if (!_javaScriptEvaluator.ValidateExpression(request.Expression, out var message))
        {
            throw new ValidationException($"Invalid expression: {message}");
        }
        var alert = new Alert
        {
            Symbol = request.Symbol.ToUpper(),
            Interval = request.Interval,
            Expression = Regex.Replace(request.Expression, @"\s+", ""),
            Status = Status.Running,
            LastNotification = DateTime.UtcNow,
        };
        alert.Add();
        await _alertRepository.AddAsync(alert, cancellationToken);
        return alert;
    }
}
