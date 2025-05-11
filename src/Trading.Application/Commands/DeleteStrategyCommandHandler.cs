using MediatR;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class DeleteStrategyCommandHandler : IRequestHandler<DeleteStrategyCommand, bool>
{
    private readonly IStrategyRepository _strategyRepository;

    public DeleteStrategyCommandHandler(IStrategyRepository strategyRepository)
    {
        _strategyRepository = strategyRepository;
    }

    public async Task<bool> Handle(DeleteStrategyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var strategy = await _strategyRepository.GetByIdAsync(request.Id, cancellationToken);
        var result = false;
        if (strategy != null)
        {
            strategy.Delete();
            result = await _strategyRepository.DeleteAsync(strategy, cancellationToken);
        }
        return result;
    }
}
