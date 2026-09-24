namespace TcgStockBot.Library.Enums;

/// <summary>
/// Why a store listing does or does not satisfy a stock task.
/// </summary>
public enum MatchOutcome
{
    /// <summary>English, in stock and at or below the maximum price.</summary>
    Match,

    /// <summary>In stock and at or below the maximum price, but the title has no language marker and the store asks to confirm it.</summary>
    MatchLanguageToConfirm,

    /// <summary>A required keyword is missing from the title.</summary>
    MissingKeyword,

    /// <summary>An excluded keyword is in the title.</summary>
    ExcludedKeyword,

    /// <summary>English task: the title marks another language.</summary>
    NotEnglish,

    /// <summary>Japanese task: the title does not mark Japanese.</summary>
    NotJapanese,

    /// <summary>The title has no language marker and the store is set not to alert on those.</summary>
    LanguageUnknown,

    /// <summary>The store shows it as out of stock.</summary>
    OutOfStock,

    /// <summary>The store shows no price.</summary>
    NoPrice,

    /// <summary>The price is above the task's maximum.</summary>
    AboveMaxPrice,
}

/// <summary>
/// Helpers for <see cref="MatchOutcome"/>.
/// </summary>
public static class MatchOutcomeExtensions
{
    /// <summary>
    /// Whether the outcome produces an alert.
    /// </summary>
    /// <param name="outcome">The outcome to test.</param>
    /// <returns>True for <see cref="MatchOutcome.Match"/> and <see cref="MatchOutcome.MatchLanguageToConfirm"/>.</returns>
    public static bool IsMatch(this MatchOutcome outcome) =>
        outcome is MatchOutcome.Match or MatchOutcome.MatchLanguageToConfirm;
}
