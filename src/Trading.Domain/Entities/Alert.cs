using Trading.Common.Enums;
using Trading.Domain.Events;

namespace Trading.Domain.Entities;

public class Alert : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public string Interval { set; get; } = "4h";
    public string Expression { get; set; } = null!; // JavaScript条件代码
    public Status Status { get; set; } = Status.Running;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;

    public void Add()
    {
        AddDomainEvent(new AlertCreatedEvent(this));
    }
    public void Pause()
    {
        Status = Status.Paused;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new AlertPausedEvent(this));
    }
    public void Resume()
    {
        Status = Status.Running;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new AlertResumedEvent(this));
    }
    public void Delete()
    {
        AddDomainEvent(new AlertDeletedEvent(this));
    }
}
