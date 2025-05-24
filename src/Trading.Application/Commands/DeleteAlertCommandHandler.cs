using MediatR;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class DeleteAlertCommandHandler : IRequestHandler<DeleteAlertCommand, bool>
{
    private readonly IAlertRepository _alertRepository;

    public DeleteAlertCommandHandler(IAlertRepository alertRepository)
    {
        _alertRepository = alertRepository;
    }

    public async Task<bool> Handle(DeleteAlertCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var strategy = await _alertRepository.GetByIdAsync(request.Id, cancellationToken);
        var result = false;
        if (strategy == null)
        {
            return result;
        }
        strategy.Delete();
        result = await _alertRepository.DeleteAsync(strategy, cancellationToken);
        return result;
    }
}
