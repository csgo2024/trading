using MediatR;
using Trading.Domain;

namespace Trading.Infrastructure;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(BaseEntity aggregateRoot);
}

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;

    public DomainEventDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task DispatchAsync(BaseEntity aggregateRoot)
    {
        var events = aggregateRoot.DomainEvents.ToList();
        foreach (var domainEvent in events)
        {
            await _mediator.Publish(domainEvent);
        }
        aggregateRoot.ClearDomainEvents();
    }
}
