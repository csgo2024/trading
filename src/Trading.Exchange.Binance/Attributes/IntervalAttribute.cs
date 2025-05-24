using System.ComponentModel.DataAnnotations;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Exchange.Binance.Attributes;

public class IntervalAttribute : ValidationAttribute
{
    public bool Required { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string interval || string.IsNullOrWhiteSpace(interval))
        {
            return Required
                ? new ValidationResult("Interval is required.")
                : ValidationResult.Success;
        }

        return BinanceHelper.KlineIntervalDict.ContainsKey(interval)
            ? ValidationResult.Success
            : new ValidationResult("Invalid interval. Must be one of: " + string.Join(", ", BinanceHelper.KlineIntervalDict.Keys));
    }
}
