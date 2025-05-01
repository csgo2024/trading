using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskCategory
{
    [Description("Alert")]
    Alert,

    [Description("Strategy")]
    Strategy,
}
