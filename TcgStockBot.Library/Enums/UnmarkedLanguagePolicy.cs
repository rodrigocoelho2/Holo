namespace TcgStockBot.Library.Enums;

/// <summary>
/// What a store's product title with no language marker ("(EN)", "Inglês", "(PT)"…) means.
/// </summary>
public enum UnmarkedLanguagePolicy
{
    /// <summary>The language is unknown: never alert.</summary>
    Unknown,

    /// <summary>The store only sells the English print run: treat as English.</summary>
    English,

    /// <summary>Alert anyway, flagged "confirm the language on the product page".</summary>
    Confirm,
}
