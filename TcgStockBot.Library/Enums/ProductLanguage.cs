namespace TcgStockBot.Library.Enums;

/// <summary>
/// The language a product title indicates.
/// </summary>
public enum ProductLanguage
{
    /// <summary>No trustworthy marker, or markers of more than one language.</summary>
    Unknown,

    /// <summary>English print run.</summary>
    English,

    /// <summary>Any language other than English and Japanese (PT, ES, FR, KR…).</summary>
    NonEnglish,

    /// <summary>Japanese print run.</summary>
    Japanese,
}
