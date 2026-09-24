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

    /// <summary>Gets or sets the CSS selectors (HTML platform only).</summary>
    public StoreHtmlSelectorDTO? Html { get; set; }

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
        Html = Platform == StorePlatform.Html ? Html?.StoreHtmlSelectorNew() : null,
    };
}
