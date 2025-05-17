using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.JavaScript;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class CreateStrategyCommandHandler : IRequestHandler<CreateStrategyCommand, Strategy>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly ILogger<CreateStrategyCommandHandler> _logger;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    public CreateStrategyCommandHandler(IStrategyRepository strategyRepository,
            JavaScriptEvaluator javaScriptEvaluator,
                                        ILogger<CreateStrategyCommandHandler> logger)
    {
        _logger = logger;
        _javaScriptEvaluator = javaScriptEvaluator;
        _strategyRepository = strategyRepository;
    }

    public async Task<Strategy> Handle(CreateStrategyCommand request, CancellationToken cancellationToken)
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
        // Validate stoploss expression
        if (!string.IsNullOrEmpty(request.StopLossExpression)
            && !_javaScriptEvaluator.ValidateExpression(request.StopLossExpression, out var message))
        {
            throw new ValidationException($"Invalid stoploss expression: {message}");
        }
        if (request.AccountType == AccountType.Spot)
        {
            if (request.StrategyType == StrategyType.TopSell || request.StrategyType == StrategyType.CloseSell)
            {
                throw new ValidationException("Spot account type is not supported for TopSell or CloseSell strategies.");
            }
        }
        var entity = new Strategy(
            request.Symbol.ToUpper(),
            request.Amount,
            request.Volatility,
            request.Leverage,
            request.AccountType,
            request.Interval,
            request.StrategyType,
            request.StopLossExpression
        );
        await _strategyRepository.Add(entity, cancellationToken);
        _logger.LogInformation("[{Interval}-{StrategyType}] Strategy created: {StrategyId}",
                               entity.Interval,
                               entity.StrategyType,
                               entity.Id);
        return entity;
    }
}
