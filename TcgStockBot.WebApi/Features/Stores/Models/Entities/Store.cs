using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Stores.Models.Entities;

/// <summary>
/// A store the bot searches, as stored in bot-config.json. Never leaves the service layer — map to <c>StoreDTO</c>.
/// </summary>
public sealed class Store
{
    /// <summary>Gets or sets the unique display name (also the identifier).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the store is searched.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the e-commerce platform.</summary>
    public StorePlatform Platform { get; set; }

    /// <summary>Gets or sets the store root, e.g. https://example-shop.pt.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets what a title with no language marker means.</summary>
    public UnmarkedLanguagePolicy UnmarkedLanguage { get; set; } = UnmarkedLanguagePolicy.Unknown;

    /// <summary>Gets or sets the WooCommerce Store API products path.</summary>
    public string WooCommerceProductsPath { get; set; } = "/wp-json/wc/store/v1/products";

    /// <summary>Gets or sets the CSS selectors (HTML platform only).</summary>
    public StoreHtmlSelector? Html { get; set; }
}

/// <summary>
/// CSS selectors used to read a server-rendered search results page.
/// </summary>
public sealed class StoreHtmlSelector
{
    /// <summary>Gets or sets the search URL with a {query} placeholder, or a fixed category page URL.</summary>
    public string SearchUrlTemplate { get; set; } = string.Empty;

    /// <summary>Gets or sets the selector of one product card.</summary>
    public string ProductSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the title selector inside a card (empty = card text).</summary>
    public string TitleSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the price selector inside a card.</summary>
    public string PriceSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets the link selector inside a card (empty = first a[href]).</summary>
    public string LinkSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets a selector that only exists when the product is out of stock.</summary>
    public string OutOfStockSelector { get; set; } = string.Empty;

    /// <summary>Gets or sets a selector that only exists when the product can be bought.</summary>
    public string InStockSelector { get; set; } = string.Empty;
}
