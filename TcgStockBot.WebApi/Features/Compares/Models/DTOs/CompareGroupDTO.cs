using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Compares.Models.DTOs;

/// <summary>
/// One product across stores: the listings grouped by set (the task that found them) and kind of product.
/// </summary>
public sealed class CompareGroupDTO
{
    /// <summary>Gets or sets the group key ("task|kind|variant", or "solo|history-key" for a separated listing).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the name shown ("Pitch Black · Elite Trainer Box").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the set name (from the task).</summary>
    public string SetName { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of product ("Elite Trainer Box").</summary>
    public string KindLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the stores' listings of this product.</summary>
    public List<CompareListingDTO> Listings { get; set; } = [];

    /// <summary>Gets or sets the MSRP reference price in euros (null when unknown).</summary>
    public decimal? Msrp { get; set; }

    /// <summary>Gets or sets how trustworthy the MSRP is.</summary>
    public MsrpBasis? MsrpBasis { get; set; }

    /// <summary>Gets or sets which reference price was used ("Pokémon · Elite Trainer Box").</summary>
    public string MsrpLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the card game (from the task that found the product).</summary>
    public TcgGame Game { get; set; } = TcgGame.Pokemon;

    /// <summary>Gets or sets the product kind code ("etb").</summary>
    public string Kind { get; set; } = "other";

    /// <summary>Gets or sets the task (watched product) the group comes from, when it still exists.</summary>
    public string? StockTaskId { get; set; }

    /// <summary>Gets or sets the id of the set (built-in or from a release), or null when the set is not known.</summary>
    public string? SetId { get; set; }

    /// <summary>Gets or sets the set code ("ME05", "OP-13"), when known.</summary>
    public string? SetCode { get; set; }

    /// <summary>Gets or sets the set's release date, when known (newest set first in the catalogue).</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>Gets or sets what tells this product apart from others of the same set and kind ("Luxray"), or empty.</summary>
    public string VariantLabel { get; set; } = string.Empty;
}

/// <summary>
/// One store's listing of a compared product.
/// </summary>
public sealed class CompareListingDTO
{
    /// <summary>Gets or sets the price history key (see PriceHistoryGet and CompareAssign).</summary>
    public string HistoryKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the store name.</summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Gets or sets the store's title of the product.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the product page URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the current price.</summary>
    public decimal? CurrentPrice { get; set; }

    /// <summary>Gets or sets a value indicating whether it can be bought now.</summary>
    public bool CurrentInStock { get; set; }

    /// <summary>Gets or sets the lowest price seen on this store.</summary>
    public decimal? LowestPrice { get; set; }

    /// <summary>Gets or sets when the lowest price was first seen.</summary>
    public DateTimeOffset? LowestPriceAt { get; set; }

    /// <summary>Gets or sets the price before the most recent drop.</summary>
    public decimal? LastDropFrom { get; set; }

    /// <summary>Gets or sets the price after the most recent drop.</summary>
    public decimal? LastDropTo { get; set; }

    /// <summary>Gets or sets when the most recent drop happened.</summary>
    public DateTimeOffset? LastDropAt { get; set; }

    /// <summary>Gets or sets when the product was last seen.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets a value indicating whether this listing is in the group by a manual correction.</summary>
    public bool IsManual { get; set; }

    /// <summary>Gets or sets when the bot first saw this listing.</summary>
    public DateTimeOffset FirstSeen { get; set; }

    /// <summary>Gets or sets the price 30 days ago (the price then in force; the first price when the listing is newer).</summary>
    public decimal? Price30DaysAgo { get; set; }

    /// <summary>Gets or sets the last product photo URL the store returned.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Gets or sets the price/stock changes, oldest first (only in CompareGet; empty in CompareList).</summary>
    public List<PricePointDTO> Points { get; set; } = [];
}

/// <summary>
/// Input of CompareAssign: moves one listing to another group, to a group of its own, or back to automatic.
/// </summary>
public sealed class CompareAssignRequest
{
    /// <summary>Gets or sets the listing's price history key.</summary>
    public string HistoryKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the target: a group key, "solo" (a product of its own) or empty (back to the automatic group).</summary>
    public string GroupKey { get; set; } = string.Empty;
}
