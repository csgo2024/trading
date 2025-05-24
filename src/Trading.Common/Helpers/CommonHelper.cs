namespace Trading.Common.Helpers;

public static class CommonHelper
{
    public static decimal TrimEndZero(decimal value)
    {
        return decimal.Parse(value.ToString("0.0##############").TrimEnd('0'));
    }
}
