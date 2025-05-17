using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class CreateStrategyCommandHandler : IRequestHandler<CreateStrategyCommand, Strategy>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly ILogger<CreateStrategyCommandHandler> _logger;
    public CreateStrategyCommandHandler(IStrategyRepository strategyRepository,
                                        ILogger<CreateStrategyCommandHandler> logger)
    {
        _logger = logger;
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
