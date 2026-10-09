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

    /// <summary>
    /// Gets or sets the Shopify catalog to read instead of the search (comma-separated): "/collections/pokemon",
    /// "/collections/a, /collections/b" or "/products.json" for the whole store. Empty = predictive search (max 10 results).
    /// </summary>
    public string ShopifyCatalogPaths { get; set; } = string.Empty;

    /// <summary>Gets or sets the CSS selectors (HTML platform only).</summary>
    public StoreHtmlSelector? Html { get; set; }

    /// <summary>
    /// Gets or sets the feed key (Awin platform only): the feed URL is read from AwinFeeds:Feeds:{key}
    /// (in Docker: AwinFeeds__Feeds__{key}).
    /// </summary>
    public string AwinFeedKey { get; set; } = string.Empty;

    /// <summary>Gets or sets where the store sells from (dashboard grouping).</summary>
    public StoreCountry Country { get; set; } = StoreCountry.Portugal;

    /// <summary>Gets or sets the MSRP tier (dashboard label).</summary>
    public StoreTier Tier { get; set; } = StoreTier.None;

    /// <summary>Gets or sets what kind of shop it is (dashboard label).</summary>
    public StoreType Type { get; set; } = StoreType.Unknown;

    /// <summary>Gets or sets free labels ("por testar", "envia para PT", "JP").</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Gets or sets free notes (what is left to configure, why it is off).</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the result of the last test from the dashboard (null = never tested).</summary>
    public StoreTestRecord? LastTest { get; set; }

    /// <summary>Gets or sets how many check cycles in a row every search on this store failed.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>Gets or sets when the bot turned the store off after too many failed cycles (null = not by the bot).</summary>
    public DateTimeOffset? AutoDisabledAt { get; set; }
}

/// <summary>
/// The result of one store test (a search with a test text), kept on the store.
/// </summary>
public sealed class StoreTestRecord
{
    /// <summary>Gets or sets when the test ran (UTC).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the outcome.</summary>
    public StoreTestStatus Status { get; set; }

    /// <summary>Gets or sets the search text used.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Gets or sets how many products the store returned.</summary>
    public int Results { get; set; }

    /// <summary>Gets or sets how many of them a task recognises (right keywords and language).</summary>
    public int Relevant { get; set; }

    /// <summary>Gets or sets how many of them would alert now (in stock, at price).</summary>
    public int Matches { get; set; }

    /// <summary>Gets or sets one returned product title, as an example.</summary>
    public string Sample { get; set; } = string.Empty;

    /// <summary>Gets or sets how long the test took.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets the translated explanation of a failure (empty on success).</summary>
    public string Message { get; set; } = string.Empty;
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
