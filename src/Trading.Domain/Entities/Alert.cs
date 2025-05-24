using Trading.Common.Enums;
using Trading.Domain.Events;

namespace Trading.Domain.Entities;

public class Alert : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public string Interval { set; get; } = "4h";
    public string Expression { get; set; } = null!;
    public Status Status { get; set; } = Status.Running;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;
    public Alert()
    {

    }
    public Alert(string symbol, string interval, string expression)
    {
        Symbol = symbol;
        Interval = interval;
        Expression = expression;
        Status = Status.Running;
        LastNotification = DateTime.UtcNow;
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
