using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.StockTasks.Services;

/// <summary>
/// Decides whether a store listing is the product a task asks for: keywords, the task's language (English or Japanese),
/// in stock and at or below the maximum price.
/// </summary>
internal static class StockTaskMatcher
{
    /// <summary>
    /// Evaluates one listing against one task.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="listing">The listing as the store returned it.</param>
    /// <param name="unmarkedLanguage">The store's policy for titles without a language marker.</param>
    /// <returns>The outcome; <see cref="MatchOutcomeExtensions.IsMatch"/> tells whether it alerts.</returns>
    public static MatchOutcome Evaluate(StockTask task, StoreListing listing, UnmarkedLanguagePolicy unmarkedLanguage)
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

        if (listing.Price > task.MaxPrice)
        {
            return MatchOutcome.AboveMaxPrice;
        }

        return languageToConfirm && unmarkedLanguage == UnmarkedLanguagePolicy.Confirm
            ? MatchOutcome.MatchLanguageToConfirm
            : MatchOutcome.Match;
    }

    /// <summary>Whole-word phrase match, so "box" does not match "boxes".</summary>
    private static bool ContainsPhrase(string normalizedTitle, string keyword)
    {
        var phrase = string.Join(' ', TextNormalizer.Words(keyword));
        return phrase.Length == 0 || $" {normalizedTitle} ".Contains($" {phrase} ", StringComparison.Ordinal);
    }
}
