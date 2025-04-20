using MediatR;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Commands;

public class DeleteAlarmCommandHandler : IRequestHandler<DeleteAlarmCommand, bool>
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly IMediator _mediator;

    public DeleteAlarmCommandHandler(IAlarmRepository alarmRepository,
                                        IMediator mediator)
    {
        _mediator = mediator;
        _alarmRepository = alarmRepository;
    }

    public async Task<bool> Handle(DeleteAlarmCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _alarmRepository.DeleteAsync(request.Id, cancellationToken);
        if (result)
        {
            await _mediator.Publish(new AlarmDeletedEvent(request.Id), cancellationToken);
        }
        return result;
    }
}
