using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Domain.Entities;

public class PriceAlert : BaseEntity
{
    public string Symbol { get; set; } = null!;
    public decimal TargetPrice { get; set; }
    public AlertType Type { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastNotification { get; set; } = DateTime.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertType
{
    [Description("Above")]
    Above,
    [Description("Below")]
    Below
}
