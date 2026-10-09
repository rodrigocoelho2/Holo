namespace TcgStockBot.Library.Enums;

/// <summary>
/// The e-commerce platform a store runs on, which decides how the bot searches it.
/// </summary>
public enum StorePlatform
{
    /// <summary>Shopify public predictive search (/search/suggest.json).</summary>
    Shopify,

    /// <summary>WooCommerce Store API (/wp-json/wc/store/…).</summary>
    WooCommerce,

    /// <summary>Server-rendered search/category page read with CSS selectors.</summary>
    Html,

    /// <summary>Amazon, through the official Creators API (credentials required).</summary>
    Amazon,

    /// <summary>FNAC Portugal, only through an authorized Awin product feed (none verified; see README).</summary>
    Fnac,

    /// <summary>Worten, through its Awin affiliate product feed (when the programme offers one).</summary>
    Worten,

    /// <summary>El Corte Inglés (Spain), through its Awin affiliate product feed.</summary>
    ElCorteIngles,

    /// <summary>
    /// Any other retailer with an Awin affiliate product feed; the feed URL is AwinFeeds:Feeds:{store's feed key}
    /// (MediaMarkt, FNAC ES, Casa del Libro…).
    /// </summary>
    Awin,

    /// <summary>No integration: the store is listed (link, tags) but never searched by the bot.</summary>
    Manual,
}
