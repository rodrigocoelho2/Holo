using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Stores.Models.DTOs;

/// <summary>
/// Output of StoreDetect: the suggested configuration of a store.
/// </summary>
public sealed class StoreDetectResult
{
    /// <summary>Gets or sets the outcome.</summary>
    public StoreDetectStatus Status { get; set; }

    /// <summary>Gets or sets the suggested platform (Manual when nothing searchable was found).</summary>
    public StorePlatform Platform { get; set; } = StorePlatform.Manual;

    /// <summary>Gets or sets the store root after redirects (https://store.pt).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets a name suggested from the site (og:site_name or the page title).</summary>
    public string SuggestedName { get; set; } = string.Empty;

    /// <summary>Gets or sets the Shopify collection found for the catalog mode (empty = search mode).</summary>
    public string ShopifyCatalogPaths { get; set; } = string.Empty;

    /// <summary>Gets or sets the WooCommerce Store API path that answered.</summary>
    public string WooCommerceProductsPath { get; set; } = "/wp-json/wc/store/v1/products";

    /// <summary>Gets or sets the suggested CSS selectors (HTML / PrestaShop only).</summary>
    public StoreHtmlSelectorDTO? Html { get; set; }

    /// <summary>Gets or sets a translated one-line explanation.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets what was checked, one line per request ("/products.json → 200 JSON").</summary>
    public List<string> Evidence { get; set; } = [];
}
