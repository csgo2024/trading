using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StrategyType
{
    [Description("DCA")]
    DCA,

    [Description("TopSell")]
    TopSell,

    [Description("BottomBuy")]
    BottomBuy,

    [Description("CloseBuy")]
    CloseBuy,

    [Description("CloseSell")]
    CloseSell,

}
