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
        Html = entity.Html is null ? null : new StoreHtmlSelectorDTO(entity.Html);
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

    /// <summary>Gets or sets the CSS selectors (HTML platform only).</summary>
    public StoreHtmlSelectorDTO? Html { get; set; }
}
