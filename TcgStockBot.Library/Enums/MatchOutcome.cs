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

    /// <summary>The task only wants some product kinds (ETB, Booster Bundle…) and this listing is another kind.</summary>
    OtherKind,

    /// <summary>MSRP rule: there is no MSRP for this product kind and the task has no fixed maximum to fall back on.</summary>
    NoMsrp,
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

    /// <summary>
    /// Whether the task recognises the product (right keywords and language), whatever its stock or price — as
    /// opposed to the unrelated results a store search also returns.
    /// </summary>
    /// <param name="outcome">The outcome to test.</param>
    /// <returns>False for missing / excluded keywords and the wrong language.</returns>
    public static bool IsRelevant(this MatchOutcome outcome) =>
        outcome is not (MatchOutcome.MissingKeyword or MatchOutcome.ExcludedKeyword or MatchOutcome.NotEnglish or MatchOutcome.NotJapanese or MatchOutcome.OtherKind);
}
