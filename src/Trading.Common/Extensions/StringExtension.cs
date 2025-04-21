using System.Text;

namespace Trading.Common.Extensions;

public static class StringExtension
{
    /// <summary>
    /// Convert string to snake case.
    /// </summary>
    /// <param name="str">String to convert.</param>
    /// <returns>Input string converted to snake case.</returns>
    public static string ToSnakeCase(this string str)
    {
        int len = str.Length;
        var sb = new StringBuilder(2 * len);
        for (int i = 0; i < len; i++)
        {
            if (i > 0 && char.IsUpper(str[i]) &&
                (char.IsLower(str[i - 1]) || i < len - 1 && char.IsLower(str[i + 1])))
            {
                sb.Append('_');

            }
            sb.Append(char.ToLower(str[i]));
        }

        return sb.ToString();
    }

    public static string ToTelegramSafeString(this string str)
    {
        return str.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

}
