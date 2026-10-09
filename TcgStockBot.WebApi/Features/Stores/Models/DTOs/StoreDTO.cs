using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Read model of a store.
/// </summary>
public sealed class StoreDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public StoreDTO()
    {
    }

    /// <summary>
    /// Maps an entity to the read model.
    /// </summary>
    /// <param name="entity">The stored store.</param>
    public StoreDTO(Store entity)
    {
        Name = entity.Name;
        Enabled = entity.Enabled;
        Platform = entity.Platform;
        BaseUrl = entity.BaseUrl;
        UnmarkedLanguage = entity.UnmarkedLanguage;
        WooCommerceProductsPath = entity.WooCommerceProductsPath;
        ShopifyCatalogPaths = entity.ShopifyCatalogPaths;
        Html = entity.Html is null ? null : new StoreHtmlSelectorDTO(entity.Html);
        AwinFeedKey = entity.AwinFeedKey;
        Country = entity.Country;
        Tier = entity.Tier;
        Type = entity.Type;
        Tags = [.. entity.Tags];
        Notes = entity.Notes;
        LastTest = entity.LastTest is null ? null : new StoreTestResultDTO(entity.Name, entity.LastTest);
        ConsecutiveFailures = entity.ConsecutiveFailures;
        AutoDisabledAt = entity.AutoDisabledAt;
    }

    /// <summary>Gets or sets the unique name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the store is searched.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the platform.</summary>
    public StorePlatform Platform { get; set; }

    /// <summary>Gets or sets the store root URL.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets what a title with no language marker means.</summary>
    public UnmarkedLanguagePolicy UnmarkedLanguage { get; set; }

    /// <summary>Gets or sets the WooCommerce Store API path.</summary>
    public string WooCommerceProductsPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the Shopify catalog paths read instead of the search (empty = search).</summary>
    public string ShopifyCatalogPaths { get; set; } = string.Empty;

    /// <summary>Gets or sets the CSS selectors (HTML platform only).</summary>
    public StoreHtmlSelectorDTO? Html { get; set; }

    /// <summary>Gets or sets the Awin feed key (Awin platform only).</summary>
    public string AwinFeedKey { get; set; } = string.Empty;

    /// <summary>Gets or sets where the store sells from.</summary>
    public StoreCountry Country { get; set; }

    /// <summary>Gets or sets the MSRP tier.</summary>
    public StoreTier Tier { get; set; }

    /// <summary>Gets or sets what kind of shop it is.</summary>
    public StoreType Type { get; set; }

    /// <summary>Gets or sets the free labels.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Gets or sets the free notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the result of the last test (null = never tested).</summary>
    public StoreTestResultDTO? LastTest { get; set; }

    /// <summary>Gets or sets how many check cycles in a row every search on this store failed.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Gets or sets when the bot turned the store off after too many failed cycles.</summary>
    public DateTimeOffset? AutoDisabledAt { get; set; }
}
