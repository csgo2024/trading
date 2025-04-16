using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;

namespace Trading.Application.Helpers;

public static class CommonHelper
{
    public static Dictionary<string, KlineInterval> KlineIntervalDict = new()
    {
        {"5m", KlineInterval.FiveMinutes},
        {"15m", KlineInterval.FifteenMinutes},
        {"1h", KlineInterval.OneHour},
        {"4h", KlineInterval.FourHour},
        {"1d", KlineInterval.OneDay},
    };
    public static decimal AdjustPriceByStepSize(decimal price, BinanceSymbolPriceFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        decimal adjustedPrice = Math.Round(price / filter.TickSize, MidpointRounding.ToZero) * filter.TickSize;
        if (adjustedPrice < filter.MinPrice)
        {
            throw new InvalidOperationException($"Price must be greater than {filter.MinPrice}");
        }
        if (adjustedPrice > filter.MaxPrice)
        {
            throw new InvalidOperationException($"Price must be less than {filter.MaxPrice}");
        }
        return TrimEndZero(adjustedPrice);
    }

    public static decimal AdjustQuantityBystepSize(decimal quantity, BinanceSymbolLotSizeFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        decimal adjustedQuantity = Math.Round(quantity / filter.StepSize, MidpointRounding.ToZero) * filter.StepSize;
        if (adjustedQuantity < filter.MinQuantity)
        {
            throw new InvalidOperationException($"Quantity must be greater than {filter.MinQuantity}");
        }
        if (adjustedQuantity > filter.MaxQuantity)
        {
            throw new InvalidOperationException($"Quantity must be less than {filter.MaxQuantity}");
        }
        return TrimEndZero(adjustedQuantity);
    }
    public static decimal TrimEndZero(decimal value)
    {
        return decimal.Parse(value.ToString("0.0##############").TrimEnd('0'));
    }

    public static KlineInterval ConvertToKlineInterval(string interval)
    {
        if (KlineIntervalDict.TryGetValue(interval, out var klineInterval))
        {
            return klineInterval;
        }
        throw new InvalidOperationException("Invalid interval");
    }
}
