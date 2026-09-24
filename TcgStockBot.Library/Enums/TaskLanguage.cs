namespace TcgStockBot.Library.Enums;

/// <summary>
/// The print run a stock task wants.
/// </summary>
public enum TaskLanguage
{
    /// <summary>English (titles without a marker follow the store's policy).</summary>
    English,

    /// <summary>Japanese (the title must say so: JP, Japanese, Japonês or Japanese script).</summary>
    Japanese,
}
