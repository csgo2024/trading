using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountType
{
    [Description("Spot")]
    Spot,

    [Description("Future")]
    Future,
}
