namespace TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

/// <summary>
/// The price history of one product on one store, as stored in price-history.json (key: <see cref="PriceObservation.KeyFor"/>).
/// Property names are the file format — do not rename.
/// </summary>
public sealed class PriceHistoryEntry
{
    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets the latest product title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the product page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the store's product identifier (ASIN, SKU, EAN…), when known.</summary>
    public string? ProductId { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency of the prices.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets when the product was first seen (UTC).</summary>
    public DateTimeOffset FirstSeen { get; set; }

    /// <summary>Gets or sets when the product was last seen (UTC).</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets the changes, oldest first: a point is added only when the price or the stock changes.</summary>
    public List<PricePoint> Points { get; set; } = [];

    /// <summary>Gets or sets the stock tasks that recognised this product (its set, for the Compare view).</summary>
    public List<string> StockTaskIds { get; set; } = [];
}

/// <summary>
/// The price and stock of a product from a given moment until the next point.
/// </summary>
public sealed class PricePoint
{
    /// <summary>Gets or sets when this price/stock was first seen (UTC).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets a value indicating whether it could be bought.</summary>
    public bool InStock { get; set; }
}
