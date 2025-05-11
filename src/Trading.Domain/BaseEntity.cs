using MediatR;

namespace Trading.Domain;

public abstract class BaseEntity : IEntity
{
    public string Id { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    private readonly List<INotification> _domainEvents = new();
    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(INotification domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
