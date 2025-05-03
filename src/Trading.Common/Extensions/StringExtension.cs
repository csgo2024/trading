using System.Buffers;
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

    private static ReadOnlySpan<byte> AmpBytes => "&amp;"u8;
    private static ReadOnlySpan<byte> LtBytes => "&lt;"u8;
    private static ReadOnlySpan<byte> GtBytes => "&gt;"u8;

    public static string ToTelegramSafeString(this string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        // Get the UTF8 byte count
        int byteCount = Encoding.UTF8.GetByteCount(str);

        // Rent buffer from array pool
        byte[] rentedArray = ArrayPool<byte>.Shared.Rent(byteCount * 5); // Max possible expansion
        try
        {
            Span<byte> bytes = rentedArray.AsSpan(0, byteCount);
            int bytesWritten = Encoding.UTF8.GetBytes(str, bytes);

            int escapedLength = 0;
            bool needsEscaping = false;

            // Count needed space and check if escaping needed
            for (int i = 0; i < bytesWritten; i++)
            {
                switch (bytes[i])
                {
                    case (byte)'&':
                        escapedLength += 5; // &amp;
                        needsEscaping = true;
                        break;
                    case (byte)'<':
                    case (byte)'>':
                        escapedLength += 4; // &lt; or &gt;
                        needsEscaping = true;
                        break;
                    default:
                        escapedLength++;
                        break;
                }
            }

            if (!needsEscaping)
            {
                return str;
            }

            // Create result buffer
            byte[] resultArray = ArrayPool<byte>.Shared.Rent(escapedLength);
            try
            {
                var result = resultArray.AsSpan(0, escapedLength);
                int writePos = 0;

                // Perform escaping
                for (int i = 0; i < bytesWritten; i++)
                {
                    switch (bytes[i])
                    {
                        case (byte)'&':
                            AmpBytes.CopyTo(result[writePos..]);
                            writePos += 5;
                            break;
                        case (byte)'<':
                            LtBytes.CopyTo(result[writePos..]);
                            writePos += 4;
                            break;
                        case (byte)'>':
                            GtBytes.CopyTo(result[writePos..]);
                            writePos += 4;
                            break;
                        default:
                            result[writePos++] = bytes[i];
                            break;
                    }
                }

                return Encoding.UTF8.GetString(result);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(resultArray);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedArray);
        }
    }
}
