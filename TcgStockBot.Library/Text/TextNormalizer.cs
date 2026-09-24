using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TcgStockBot.Library.Text;

/// <summary>
/// Accent- and case-insensitive text helpers used for keyword matching.
/// </summary>
public static partial class TextNormalizer
{
    /// <summary>Lower-cases and strips accents: "Pokémon Inglês" → "pokemon ingles".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Splits normalized text into alphanumeric words.</summary>
    public static IReadOnlyList<string> Words(string text) =>
        NonWordCharacters().Split(Normalize(text))
            .Where(static word => word.Length > 0)
            .ToList();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonWordCharacters();
}
