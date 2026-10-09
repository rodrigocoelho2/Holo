namespace TcgStockBot.Library.Enums;

/// <summary>
/// The outcome of testing a store (one search with a test text).
/// </summary>
public enum StoreTestStatus
{
    /// <summary>The store answered with products.</summary>
    Ok,

    /// <summary>The store answered, but with no products for the test text.</summary>
    Empty,

    /// <summary>The store refused the request (HTTP 403, usually anti-bot protection; never bypassed).</summary>
    Blocked,

    /// <summary>The store asked to slow down (HTTP 429), or the bot's own per-domain limit postponed the request.</summary>
    RateLimited,

    /// <summary>The store could not be reached (DNS, connection, timeout).</summary>
    Unreachable,

    /// <summary>The store has no integration (Manual platform): it is never searched.</summary>
    NoIntegration,

    /// <summary>The provider needs credentials or a feed that are not configured.</summary>
    NotConfigured,

    /// <summary>Any other failure (unexpected answer, wrong selectors, server error).</summary>
    Error,
}
