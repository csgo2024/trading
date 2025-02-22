using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StrategyType
{
    [Description("Spot")]
    Spot,

    [Description("Feature")]
    Feature,
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StrateStatus
{
    [Description("Paused")]
    Paused,

    [Description("Running")]
    Running,
}