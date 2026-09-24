using System.Globalization;
using System.Text.RegularExpressions;

namespace TcgStockBot.Library.Text;

/// <summary>Reads prices written the European way ("54,99 €", "1.234,50€") or the English way ("€54.99").</summary>
public static partial class PriceParser
{
    /// <summary>
    /// Extracts the first price in a text.
    /// </summary>
    /// <param name="text">Text such as "54,99 €".</param>
    /// <returns>The price, or null when the text has no number.</returns>
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = NumberPattern().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var number = match.Value.TrimEnd('.', ',');
        var lastComma = number.LastIndexOf(',');
        var lastDot = number.LastIndexOf('.');

        // The right-most separator is the decimal one when followed by exactly 1-2 digits; any other is a thousands separator.
        var decimalSeparatorIndex = Math.Max(lastComma, lastDot);
        var hasDecimals = decimalSeparatorIndex >= 0 && number.Length - decimalSeparatorIndex - 1 is 1 or 2;

        var integerPart = hasDecimals ? number[..decimalSeparatorIndex] : number;
        var fractionPart = hasDecimals ? number[(decimalSeparatorIndex + 1)..] : "0";
        var digitsOnly = integerPart.Replace(",", string.Empty).Replace(".", string.Empty);

        return decimal.TryParse($"{digitsOnly}.{fractionPart}", NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price)
            ? price
            : null;
    }

    [GeneratedRegex(@"\d[\d.,]*")]
    private static partial Regex NumberPattern();
}
