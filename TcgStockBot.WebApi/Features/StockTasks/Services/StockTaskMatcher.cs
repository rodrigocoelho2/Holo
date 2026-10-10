using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Msrps.Models.Entities;
using TcgStockBot.WebApi.Features.Msrps.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.StockTasks.Services;

/// <summary>
/// The price a listing is judged against: its product kind, the MSRP of that kind (when known) and the limit that
/// applies — the MSRP plus the task's tolerance, or the task's fixed maximum.
/// </summary>
/// <param name="Kind">The product kind of the title.</param>
/// <param name="Msrp">The MSRP entry of that kind, or null.</param>
/// <param name="Limit">The highest price that alerts, or null when there is none (MSRP rule, no MSRP, no fixed maximum).</param>
internal sealed record PriceCheck(ProductKind Kind, MsrpEntry? Msrp, decimal? Limit);

/// <summary>
/// Decides whether a store listing is the product a task asks for: keywords, product kind, the task's language
/// (English or Japanese), in stock and at or below the price limit.
/// </summary>
internal static class StockTaskMatcher
{
    /// <summary>
    /// The product kind, MSRP and price limit of one title for one task.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="title">The listing title.</param>
    /// <param name="msrp">The MSRP table.</param>
    /// <returns>The price check.</returns>
    public static PriceCheck PriceFor(StockTask task, string title, MsrpLookup? msrp)
    {
        var kind = ProductKindClassifier.Classify(title);
        var reference = (msrp ?? MsrpLookup.Empty).Find(title, $"{task.Name} {GameWords(task.Game)}");
        decimal? fixedMax = task.MaxPrice > 0 ? task.MaxPrice : null;
        var limit = task.PriceRule == PriceRule.Msrp && reference is not null
            ? Math.Round(reference.Price * (100 + task.MsrpTolerancePercent) / 100m, 2, MidpointRounding.AwayFromZero)
            : fixedMax;
        return new PriceCheck(kind, reference, limit);
    }

    /// <summary>The words of a game as the MSRP table names it ("Pokémon"), so the lookup finds the right game.</summary>
    private static string GameWords(TcgGame game) => game switch
    {
        TcgGame.Pokemon => "Pokémon",
        TcgGame.OnePiece => "One Piece",
        TcgGame.Lorcana => "Lorcana",
        TcgGame.Magic => "Magic",
        _ => string.Empty,
    };

    /// <summary>
    /// Evaluates one listing against one task.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="listing">The listing as the store returned it.</param>
    /// <param name="unmarkedLanguage">The store's policy for titles without a language marker.</param>
    /// <param name="msrp">The MSRP table (for <see cref="PriceRule.Msrp"/>); null = none.</param>
    /// <returns>The outcome; <see cref="MatchOutcomeExtensions.IsMatch"/> tells whether it alerts.</returns>
    public static MatchOutcome Evaluate(StockTask task, StoreListing listing, UnmarkedLanguagePolicy unmarkedLanguage, MsrpLookup? msrp = null)
    {
        var normalizedTitle = string.Join(' ', TextNormalizer.Words(listing.Title));

        if (!task.RequiredKeywords.All(keyword => ContainsPhrase(normalizedTitle, keyword)))
        {
            return MatchOutcome.MissingKeyword;
        }

        if (task.ExcludedKeywords.Any(keyword => ContainsPhrase(normalizedTitle, keyword)))
        {
            return MatchOutcome.ExcludedKeyword;
        }

        var price = PriceFor(task, listing.Title, msrp);
        if (task.Kinds.Count > 0 && !task.Kinds.Contains(price.Kind.Code, StringComparer.OrdinalIgnoreCase))
        {
            return MatchOutcome.OtherKind;
        }

        var language = LanguageClassifier.Classify(listing.Title);
        var languageToConfirm = false;

        if (task.Language == TaskLanguage.Japanese)
        {
            // Stores always mark the Japanese print run, so an unmarked title is not Japanese.
            if (language != ProductLanguage.Japanese)
            {
                return MatchOutcome.NotJapanese;
            }
        }
        else
        {
            if (language is ProductLanguage.NonEnglish or ProductLanguage.Japanese)
            {
                return MatchOutcome.NotEnglish;
            }

            languageToConfirm = language == ProductLanguage.Unknown;
            if (languageToConfirm && unmarkedLanguage == UnmarkedLanguagePolicy.Unknown)
            {
                return MatchOutcome.LanguageUnknown;
            }
        }

        if (!listing.InStock)
        {
            return MatchOutcome.OutOfStock;
        }

        // 0,00 € is a placeholder some stores show before a release; other currencies can't be compared with a EUR maximum.
        if (listing.Price is null or <= 0 || !string.Equals(listing.Currency, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            return MatchOutcome.NoPrice;
        }

        if (price.Limit is null)
        {
            return MatchOutcome.NoMsrp;
        }

        if (listing.Price > price.Limit)
        {
            return MatchOutcome.AboveMaxPrice;
        }

        return languageToConfirm && unmarkedLanguage == UnmarkedLanguagePolicy.Confirm
            ? MatchOutcome.MatchLanguageToConfirm
            : MatchOutcome.Match;
    }

    /// <summary>
    /// Whole-word phrase match, so "box" does not match "boxes". A keyword can list alternatives separated by "|"
    /// ("booster box|booster display" = either one), and an alternative ending in "*" matches the start of a word
    /// ("op*" matches "OP-15" and "OP10").
    /// </summary>
    private static bool ContainsPhrase(string normalizedTitle, string keyword)
    {
        var paddedTitle = $" {normalizedTitle} ";
        var alternatives = keyword.Split('|')
            .Select(static alternative => alternative.Trim())
            .Select(static alternative => (IsPrefix: alternative.EndsWith('*'), Phrase: string.Join(' ', TextNormalizer.Words(alternative.TrimEnd('*')))))
            .Where(static alternative => alternative.Phrase.Length > 0)
            .ToList();

        return alternatives.Count == 0 || alternatives.Any(alternative => alternative.IsPrefix
            ? paddedTitle.Contains($" {alternative.Phrase}", StringComparison.Ordinal)
            : paddedTitle.Contains($" {alternative.Phrase} ", StringComparison.Ordinal));
    }
}
