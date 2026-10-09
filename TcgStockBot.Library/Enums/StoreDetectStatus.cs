namespace TcgStockBot.Library.Enums;

/// <summary>
/// The outcome of detecting a store's platform.
/// </summary>
public enum StoreDetectStatus
{
    /// <summary>A searchable platform was found (Shopify, WooCommerce, PrestaShop search).</summary>
    Detected,

    /// <summary>The site refused the bot (HTTP 403 / 429 or an anti-bot challenge); nothing is bypassed.</summary>
    Blocked,

    /// <summary>The site could not be reached (DNS, connection, timeout).</summary>
    Unreachable,

    /// <summary>The site answered, but no public product API or known search page was found.</summary>
    Unknown,
}
