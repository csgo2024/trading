using MediatR;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class DeleteStrategyCommandHandler : IRequestHandler<DeleteStrategyCommand, bool>
{
    private readonly IStrategyRepository _strategyRepository;
    private readonly IMediator _mediator;

    public DeleteStrategyCommandHandler(IStrategyRepository strategyRepository,
                                        IMediator mediator)
    {
        _mediator = mediator;
        _strategyRepository = strategyRepository;
    }

    public async Task<bool> Handle(DeleteStrategyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var deletedStrategy = await _strategyRepository.GetByIdAsync(request.Id, cancellationToken);
        var result = await _strategyRepository.DeleteAsync(request.Id, cancellationToken);
        if (result && deletedStrategy != null)
        {
            await _mediator.Publish(new StrategyDeletedEvent(deletedStrategy), cancellationToken);
        }
        return result;
    }
}
