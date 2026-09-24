using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// One listing returned by StoreProbe, with the verdict of every stock task.
/// </summary>
public sealed class StoreProbeResult
{
    /// <summary>Gets or sets the product title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the product page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the price, when shown.</summary>
    public decimal? Price { get; set; }

    /// <summary>Gets or sets a value indicating whether the product can be bought.</summary>
    public bool InStock { get; set; }

    /// <summary>Gets or sets the language the title indicates.</summary>
    public ProductLanguage Language { get; set; }

    /// <summary>Gets or sets the outcome per stock task identifier.</summary>
    public Dictionary<string, MatchOutcome> OutcomeByTask { get; set; } = [];

    /// <summary>Gets or sets the store's product identifier (ASIN, SKU, EAN…), when known.</summary>
    public string? ProductId { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency of the price.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets who sells the product, when known.</summary>
    public string? Seller { get; set; }
}
