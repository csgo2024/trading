using Trading.Common.Enums;

namespace Trading.Domain.Entities;

public class Alert : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public string Interval { set; get; } = "4h";
    public string Expression { get; set; } = null!; // JavaScript条件代码
    public Status Status { get; set; } = Status.Running;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;
}
