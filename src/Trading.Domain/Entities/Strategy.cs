using Trading.Common.Enums;
using Trading.Domain.Events;

namespace Trading.Domain.Entities;

public class Strategy : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;
    public decimal? OpenPrice { get; set; }
    public decimal TargetPrice { get; set; }
    public long? OrderId { get; set; }
    public bool HasOpenOrder { get; set; }
    public bool RequireReset { get; set; } = true;
    public DateTime? OrderPlacedTime { get; set; }
    public int Amount { get; set; }
    public decimal Volatility { get; set; }

    public decimal Quantity { get; set; }
    public int? Leverage { get; set; }
    public AccountType AccountType { get; set; }

    public StrategyType StrategyType { get; set; }

    public Status Status { get; set; }
    public string? Interval { get; set; }
    public string StopLossExpression { get; set; } = string.Empty;
    public Strategy()
    {
    }

    public Strategy(
        string symbol,
        int amount,
        decimal volatility,
        int? leverage,
        AccountType accountType,
        string? interval,
        StrategyType strategyType,
        string stopLossExpression
    )
    {
        Interval = interval;
        Symbol = symbol;
        Amount = amount;
        Volatility = volatility;
        Leverage = leverage;
        AccountType = accountType;
        StrategyType = strategyType;
        Volatility = volatility;
        CreatedAt = DateTime.Now;
        Status = Status.Running;
        StopLossExpression = stopLossExpression;
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
