using Trading.Common.Enums;
using Trading.Domain.Events;

namespace Trading.Domain.Entities;

public class Strategy : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public long? OrderId { get; set; }
    public bool HasOpenOrder { get; set; }
    public DateTime? OrderPlacedTime { get; set; }
    public int Amount { get; set; }
    public decimal Volatility { get; set; }

    public decimal Quantity { get; set; }
    public int? Leverage { get; set; }
    public AccountType AccountType { get; set; }

    public StrategyType StrategyType { get; set; }

    public Status Status { get; set; }
    public string? Interval { get; set; }
    public string? StopLossExpression { get; set; }

    public void Add()
    {
        AddDomainEvent(new StrategyCreatedEvent(this));
    }
    public void Pause()
    {
        Status = Status.Paused;
        AddDomainEvent(new StrategyPausedEvent(this));
    }
    public void Resume()
    {
        Status = Status.Running;
        AddDomainEvent(new StrategyResumedEvent(this));
    }
    public void Delete()
    {
        AddDomainEvent(new StrategyDeletedEvent(this));
    }
}
