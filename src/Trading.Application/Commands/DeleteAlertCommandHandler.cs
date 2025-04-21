using MediatR;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class DeleteAlertCommandHandler : IRequestHandler<DeleteAlertCommand, bool>
{
    private readonly IAlertRepository _alertRepository;
    private readonly IMediator _mediator;

    public DeleteAlertCommandHandler(IAlertRepository alertRepository,
                                        IMediator mediator)
    {
        _mediator = mediator;
        _alertRepository = alertRepository;
    }

    public async Task<bool> Handle(DeleteAlertCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _alertRepository.DeleteAsync(request.Id, cancellationToken);
        if (result)
        {
            await _mediator.Publish(new AlertDeletedEvent(request.Id), cancellationToken);
        }
        return result;
    }
}
