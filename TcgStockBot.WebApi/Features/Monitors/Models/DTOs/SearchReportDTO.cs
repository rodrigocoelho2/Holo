using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Monitors.Models.DTOs;

/// <summary>
/// The latest search of one task on one store.
/// </summary>
public sealed class SearchReportDTO
{
    /// <summary>
    /// Gets or sets a value indicating whether a failure counts against the store for the automatic turn-off
    /// (not when the bot's own limit postponed it, or the provider is not configured). Internal: not in the API.
    /// </summary>
    internal bool CountsAgainstStore { get; set; }

    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets the task identifier.</summary>
    public string StockTaskId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the store answered.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets why the search failed.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets when the search ran.</summary>
    public DateTimeOffset CheckedAt { get; set; }

    /// <summary>Gets or sets how long the store took, in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets what the store returned, with the verdict for this task.</summary>
    public List<ListingReportDTO> Listings { get; set; } = [];

    /// <summary>Gets how many listings alert (including "language to confirm").</summary>
    public int MatchCount => Listings.Count(static listing => listing.Outcome.IsMatch());
}

/// <summary>
/// One listing of a search, with its verdict.
/// </summary>
public sealed class ListingReportDTO
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

    /// <summary>Gets or sets the verdict for the task.</summary>
    public MatchOutcome Outcome { get; set; }

    /// <summary>Gets or sets the store's product identifier (ASIN, SKU, EAN…), when known.</summary>
    public string? ProductId { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency of the price.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets who sells the product, when known.</summary>
    public string? Seller { get; set; }

    /// <summary>Gets or sets the product photo URL, when the store provides one.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Gets or sets a value indicating whether it is a pre-order (from the title or the provider's data).</summary>
    public bool PreOrder { get; set; }

    /// <summary>Gets or sets the price history key of this product (see PriceHistoryGet).</summary>
    public string PriceHistoryKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the lowest price seen for this product on this store, including now; null when unknown.</summary>
    public decimal? LowestPrice { get; set; }

    /// <summary>Gets or sets the product kind code read from the title (etb, booster-bundle…; "other" when unknown).</summary>
    public string Kind { get; set; } = "other";

    /// <summary>Gets or sets the product kind label ("Elite Trainer Box").</summary>
    public string KindLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the MSRP of the product kind, when known.</summary>
    public decimal? Msrp { get; set; }

    /// <summary>Gets or sets the highest price that alerts for this listing (task rule), or null when there is none.</summary>
    public decimal? PriceLimit { get; set; }
}
