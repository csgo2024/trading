using System.ComponentModel.DataAnnotations;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using Trading.Common.Helpers;

namespace Trading.Exchange.Binance.Helpers;

public static class BinanceHelper
{
    private static readonly Dictionary<string, KlineInterval> _stringToInterval = new(StringComparer.OrdinalIgnoreCase)
    {
        { "5m", KlineInterval.FiveMinutes },
        { "15m", KlineInterval.FifteenMinutes },
        { "1h", KlineInterval.OneHour },
        { "4h", KlineInterval.FourHour },
        { "1d", KlineInterval.OneDay },
        { "3d", KlineInterval.ThreeDay },
        { "1w", KlineInterval.OneWeek },
    };

    public static IReadOnlyDictionary<string, KlineInterval> KlineIntervalDict => _stringToInterval;

    private static readonly Dictionary<KlineInterval, string> _intervalToString =
        _stringToInterval.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static KlineInterval ConvertToKlineInterval(string interval) =>
        _stringToInterval.TryGetValue(interval, out var result)
            ? result
            : throw new ValidationException($"Invalid interval: '{interval}'");

    public static string ConvertToIntervalString(KlineInterval interval) =>
        _intervalToString.TryGetValue(interval, out var result)
            ? result
            : throw new ValidationException($"Invalid KlineInterval: '{interval}'");

    public static decimal AdjustPriceByStepSize(decimal price, BinanceSymbolPriceFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var adjustedPrice = Math.Round(price / filter.TickSize, MidpointRounding.ToZero) * filter.TickSize;
        if (adjustedPrice < filter.MinPrice)
        {
            throw new InvalidOperationException($"Price must be greater than {filter.MinPrice}");
        }
        if (adjustedPrice > filter.MaxPrice)
        {
            throw new InvalidOperationException($"Price must be less than {filter.MaxPrice}");
        }
        return CommonHelper.TrimEndZero(adjustedPrice);
    }

    public static decimal AdjustQuantityBystepSize(decimal quantity, BinanceSymbolLotSizeFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var adjustedQuantity = Math.Round(quantity / filter.StepSize, MidpointRounding.ToZero) * filter.StepSize;
        if (adjustedQuantity < filter.MinQuantity)
        {
            throw new InvalidOperationException($"Quantity must be greater than {filter.MinQuantity}");
        }
        if (adjustedQuantity > filter.MaxQuantity)
        {
            throw new InvalidOperationException($"Quantity must be less than {filter.MaxQuantity}");
        }
        return CommonHelper.TrimEndZero(adjustedQuantity);
    }
}
