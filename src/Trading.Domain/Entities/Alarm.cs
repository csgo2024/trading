
namespace Trading.Domain.Entities;

public class Alarm : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public string Interval { set; get; } = "4h";
    public string Expression { get; set; } = null!; // JavaScript条件代码
    public bool IsActive { get; set; } = true;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;
}
