
namespace Trading.Domain.Entities;

public class Alert : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public string Interval { set; get; } = "4h";
    public string Expression { get; set; } = null!; // JavaScript条件代码
    public StateStatus Status { get; set; } = StateStatus.Running;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;
}
