using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Status
{
    [Description("Paused")]
    Paused,

    [Description("Running")]
    Running,
}

public static class StatusExtensions
{
    public static (string emoji, string status) GetStatusInfo(this Status status)
    {
        return status switch
        {
            Status.Running => ("🟢", "运行中"),
            Status.Paused => ("🔴", "已暂停"),
            _ => ("⚠️", "未知状态")
        };
    }
}
