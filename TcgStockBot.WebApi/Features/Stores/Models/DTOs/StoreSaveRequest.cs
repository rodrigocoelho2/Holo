using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Input of StoreSave: creates a store (no <see cref="OriginalName"/>) or replaces / renames an existing one.
/// </summary>
public sealed class StoreSaveRequest
{
    /// <summary>Gets or sets the current name of the store being edited; null when creating.</summary>
    public string? OriginalName { get; set; }

    /// <summary>Gets or sets the unique name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the store is searched.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the platform.</summary>
    public StorePlatform Platform { get; set; }

    /// <summary>Gets or sets the store root URL.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets what a title with no language marker means.</summary>
    public UnmarkedLanguagePolicy UnmarkedLanguage { get; set; }

    /// <summary>Gets or sets the WooCommerce Store API path.</summary>
    public string WooCommerceProductsPath { get; set; } = "/wp-json/wc/store/v1/products";

    /// <summary>Gets or sets the Shopify catalog paths read instead of the search, comma-separated (empty = search).</summary>
    public string ShopifyCatalogPaths { get; set; } = string.Empty;

    /// <summary>Gets or sets the CSS selectors (HTML platform only).</summary>
    public StoreHtmlSelectorDTO? Html { get; set; }

    /// <summary>Gets or sets the Awin feed key (Awin platform only): the feed URL is AwinFeeds:Feeds:{key}.</summary>
    public string AwinFeedKey { get; set; } = string.Empty;

    /// <summary>Gets or sets where the store sells from.</summary>
    public StoreCountry Country { get; set; } = StoreCountry.Portugal;

    /// <summary>Gets or sets the MSRP tier.</summary>
    public StoreTier Tier { get; set; } = StoreTier.None;

    /// <summary>Gets or sets what kind of shop it is.</summary>
    public StoreType Type { get; set; } = StoreType.Unknown;

    /// <summary>Gets or sets free labels.</summary>
    public List<string>? Tags { get; set; } = [];

    /// <summary>Gets or sets free notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the shipping cost of one order in EUR (null = not known, 0 = always free).</summary>
    public decimal? ShippingCost { get; set; }

    /// <summary>Gets or sets the order total from which shipping is free, in EUR (null = never free).</summary>
    public decimal? FreeShippingFrom { get; set; }

    /// <summary>
    /// Maps the request to an entity; the selectors are only kept for the HTML platform.
    /// </summary>
    /// <returns>The entity to store.</returns>
    public Store StoreNew() => new()
    {
        Name = (Name ?? string.Empty).Trim(),
        Enabled = Enabled,
        Platform = Platform,
        BaseUrl = (BaseUrl ?? string.Empty).Trim(),
        UnmarkedLanguage = UnmarkedLanguage,
        WooCommerceProductsPath = string.IsNullOrWhiteSpace(WooCommerceProductsPath) ? "/wp-json/wc/store/v1/products" : WooCommerceProductsPath.Trim(),
        ShopifyCatalogPaths = Platform == StorePlatform.Shopify ? (ShopifyCatalogPaths ?? string.Empty).Trim() : string.Empty,
        Html = Platform == StorePlatform.Html ? Html?.StoreHtmlSelectorNew() : null,
        AwinFeedKey = Platform == StorePlatform.Awin ? (AwinFeedKey ?? string.Empty).Trim() : string.Empty,
        Country = Country,
        Tier = Tier,
        Type = Type,
        Tags = (Tags ?? [])
            .Select(static tag => (tag ?? string.Empty).Trim())
            .Where(static tag => tag.Length > 0)
            .DistinctBy(static tag => tag.ToLowerInvariant())
            .ToList(),
        Notes = (Notes ?? string.Empty).Trim(),
        ShippingCost = ShippingCost is null ? null : Math.Round(ShippingCost.Value, 2, MidpointRounding.AwayFromZero),
        FreeShippingFrom = FreeShippingFrom is null or 0 ? null : Math.Round(FreeShippingFrom.Value, 2, MidpointRounding.AwayFromZero),
    };
}
