using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

namespace TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;

/// <summary>
/// The price history of one product on one store, with its points.
/// </summary>
public sealed class PriceHistoryDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PriceHistoryDTO()
    {
    }

    /// <summary>
    /// Maps a stored entry.
    /// </summary>
    /// <param name="key">The history key.</param>
    /// <param name="entity">The stored entry.</param>
    public PriceHistoryDTO(string key, PriceHistoryEntry entity)
    {
        Summary = new PriceHistorySummaryDTO(key, entity);
        Points = entity.Points.Select(static point => new PricePointDTO(point)).ToList();
    }

    /// <summary>Gets or sets the summary (current, lowest and highest price).</summary>
    public PriceHistorySummaryDTO Summary { get; set; } = new();

    /// <summary>Gets or sets the changes, oldest first.</summary>
    public List<PricePointDTO> Points { get; set; } = [];
}

/// <summary>
/// One tracked product: where, current price and the range seen so far.
/// </summary>
public sealed class PriceHistorySummaryDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PriceHistorySummaryDTO()
    {
    }

    /// <summary>
    /// Maps a stored entry.
    /// </summary>
    /// <param name="key">The history key.</param>
    /// <param name="entity">The stored entry.</param>
    public PriceHistorySummaryDTO(string key, PriceHistoryEntry entity)
    {
        Key = key;
        StoreName = entity.StoreName;
        Title = entity.Title;
        Url = entity.Url;
        ProductId = entity.ProductId;
        Currency = entity.Currency;
        FirstSeen = entity.FirstSeen;
        LastSeen = entity.LastSeen;
        PointCount = entity.Points.Count;

        if (entity.Points.Count > 0)
        {
            var current = entity.Points[^1];
            var lowest = entity.Points.MinBy(static point => point.Price)!;
            CurrentPrice = current.Price;
            CurrentInStock = current.InStock;
            LastChange = current.At;
            LowestPrice = lowest.Price;
            LowestPriceAt = lowest.At;
            HighestPrice = entity.Points.Max(static point => point.Price);
        }
    }

    /// <summary>Gets or sets the history key (pass it to PriceHistoryGet).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets the latest product title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the product page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the store's product identifier, when known.</summary>
    public string? ProductId { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets when the product was first seen.</summary>
    public DateTimeOffset FirstSeen { get; set; }

    /// <summary>Gets or sets when the product was last seen.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets when the price or stock last changed.</summary>
    public DateTimeOffset? LastChange { get; set; }

    /// <summary>Gets or sets the current price.</summary>
    public decimal? CurrentPrice { get; set; }

    /// <summary>Gets or sets a value indicating whether it can be bought now.</summary>
    public bool CurrentInStock { get; set; }

    /// <summary>Gets or sets the lowest price seen.</summary>
    public decimal? LowestPrice { get; set; }

    /// <summary>Gets or sets when the lowest price was first seen.</summary>
    public DateTimeOffset? LowestPriceAt { get; set; }

    /// <summary>Gets or sets the highest price seen.</summary>
    public decimal? HighestPrice { get; set; }

    /// <summary>Gets or sets how many changes were recorded.</summary>
    public int PointCount { get; set; }
}

/// <summary>
/// One change of price or stock.
/// </summary>
public sealed class PricePointDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PricePointDTO()
    {
    }

    /// <summary>
    /// Maps a stored point.
    /// </summary>
    /// <param name="entity">The stored point.</param>
    public PricePointDTO(PricePoint entity)
    {
        At = entity.At;
        Price = entity.Price;
        InStock = entity.InStock;
    }

    /// <summary>Gets or sets when this price/stock was first seen.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets a value indicating whether it could be bought.</summary>
    public bool InStock { get; set; }
}
