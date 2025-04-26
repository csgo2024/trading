namespace Trading.Domain.Entities;

// 每个交易对的策略状态
public class Strategy : BaseEntity
{
    public string Symbol { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public long? OrderId { get; set; }
    public DateTime? LastTradeDate { get; set; }
    public bool HasOpenOrder { get; set; }
    public bool IsTradedToday { get; set; }
    public DateTime? OrderPlacedTime { get; set; }
    public int Amount { get; set; }
    public decimal Volatility { get; set; }

    public decimal Quantity { get; set; }
    public int? Leverage { get; set; }
    public AccountType AccountType { get; set; }

    public StrategyType StrategyType { get; set; }

    public StateStatus Status { get; set; }
    public string? Interval { get; set; }

}
