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
        return await _strategyRepository.DeleteAsync(request.Id, cancellationToken);
    }
}
