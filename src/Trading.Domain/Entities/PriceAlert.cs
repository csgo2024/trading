
namespace Trading.Domain.Entities;

public class PriceAlert : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public string Condition { get; set; } = null!; // JavaScript条件代码
    public bool IsActive { get; set; } = true;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;
}
