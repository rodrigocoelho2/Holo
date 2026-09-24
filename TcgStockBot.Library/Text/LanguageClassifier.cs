using System.Text.RegularExpressions;
using TcgStockBot.Library.Enums;

namespace TcgStockBot.Library.Text;

/// <summary>
/// Decides from a product title which print run it is: English, Japanese or another language.
/// Full words ("English", "Inglês", "Japanese", "Español") count anywhere in the title. Two-letter codes
/// ("EN", "PT", "DE") only count when bracketed — "(PT)", "[EN]" — or written in upper case inside a
/// mixed-case title, because "de" / "es" are ordinary Portuguese and Spanish words. "JP" / "JPN" and
/// Japanese script (hiragana, katakana, kanji) always mark Japanese.
/// A title carrying markers of more than one language is <see cref="ProductLanguage.Unknown"/>.
/// </summary>
public static partial class LanguageClassifier
{
    private static readonly HashSet<string> EnglishWords =
        ["english", "ingles", "inglesa", "eng"];

    private static readonly HashSet<string> JapaneseWords =
        ["japanese", "japones", "japonesa", "japonais", "jp", "jpn"];

    private static readonly HashSet<string> OtherLanguageWords =
    [
        "portugues", "portuguesa", "espanol", "espanhol", "espanhola", "castellano",
        "frances", "francesa", "francais", "deutsch", "alemao", "alema", "italiano", "italiana",
        "korean", "coreano", "chinese", "chines",
    ];

    private static readonly HashSet<string> EnglishCodes = ["en", "eng"];

    private static readonly HashSet<string> JapaneseCodes = ["jp", "jap", "jpn"];

    private static readonly HashSet<string> OtherLanguageCodes =
        ["pt", "es", "esp", "fr", "de", "it", "kr", "cn", "zh"];

    /// <summary>
    /// Classifies a product title.
    /// </summary>
    /// <param name="title">The title as the store shows it.</param>
    /// <returns>The language the title indicates.</returns>
    public static ProductLanguage Classify(string title)
    {
        var words = TextNormalizer.Words(title);
        var codes = LanguageCodes(title);

        var isEnglish = words.Any(EnglishWords.Contains) || codes.Any(EnglishCodes.Contains);
        var isJapanese = words.Any(JapaneseWords.Contains) || codes.Any(JapaneseCodes.Contains) || JapaneseScript().IsMatch(title);
        var isOther = words.Any(OtherLanguageWords.Contains) || codes.Any(OtherLanguageCodes.Contains);

        return (isEnglish, isJapanese, isOther) switch
        {
            (true, false, false) => ProductLanguage.English,
            (false, true, false) => ProductLanguage.Japanese,
            (false, false, true) => ProductLanguage.NonEnglish,
            _ => ProductLanguage.Unknown,
        };
    }

    /// <summary>Short codes that can be trusted as language markers (bracketed, or upper case in a mixed-case title).</summary>
    private static HashSet<string> LanguageCodes(string title)
    {
        var codes = new HashSet<string>();

        foreach (Match match in BracketedCode().Matches(title))
        {
            codes.Add(match.Groups["code"].Value.ToLowerInvariant());
        }

        var isAllUpperCase = !title.Any(char.IsLower);
        if (!isAllUpperCase)
        {
            foreach (Match match in UpperCaseCode().Matches(title))
            {
                codes.Add(match.Value.ToLowerInvariant());
            }
        }

        return codes;
    }

    [GeneratedRegex(@"[\(\[]\s*(?<code>[A-Za-z]{2,3})\s*[\)\]]")]
    private static partial Regex BracketedCode();

    [GeneratedRegex(@"(?<![\p{L}\p{Nd}])[A-Z]{2,3}(?![\p{L}\p{Nd}])")]
    private static partial Regex UpperCaseCode();

    [GeneratedRegex(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]")]
    private static partial Regex JapaneseScript();
}
