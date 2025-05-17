using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MediatR;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public partial class CreateAlertCommandHandler : IRequestHandler<CreateAlertCommand, Alert>
{
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    private readonly IAlertRepository _alertRepository;

    public CreateAlertCommandHandler(IAlertRepository alertRepository)
    {
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
        var alert = new Alert(
            request.Symbol.ToUpper(),
            request.Interval,
            WhitespaceRegex().Replace(request.Expression, "")
        );
        await _alertRepository.AddAsync(alert, cancellationToken);
        return alert;
    }
}
