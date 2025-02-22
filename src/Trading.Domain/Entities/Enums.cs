using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountType
{
    [Description("Spot")]
    Spot,

    [Description("Feature")]
    Feature,
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StateStatus
{
    [Description("Paused")]
    Paused,

    [Description("Running")]
    Running,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StrategyType
{
    [Description("DCA")]
    DCA,

    [Description("BuyBottom")]
    BuyBottom,
}