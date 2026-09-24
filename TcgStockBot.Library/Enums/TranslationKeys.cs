namespace TcgStockBot.Library.Enums;

/// <summary>
/// Keys of the user-facing messages. Use with <c>nameof(TranslationKeys.X)</c>, never a magic string.
/// </summary>
public enum TranslationKeys
{
    /// <summary>Generic title of a validation failure.</summary>
    ValidationProblems,

    /// <summary>A required field is empty.</summary>
    RequiredField,

    /// <summary>A value must be greater than zero.</summary>
    GreaterThen0,

    /// <summary>A value must be an absolute http(s) address.</summary>
    InvalidUrl,

    /// <summary>A value contains the reserved '|' character.</summary>
    InvalidCharacter,

    /// <summary>Another record already uses the value.</summary>
    AlreadyExists,

    /// <summary>The record does not exist.</summary>
    NoRecordsFound,

    /// <summary>Poll interval below the minimum.</summary>
    PollIntervalTooShort,

    /// <summary>A negative wait.</summary>
    NegativeValue,

    /// <summary>The store answered with an HTTP error.</summary>
    StoreHttpError,

    /// <summary>The store did not answer in time.</summary>
    StoreTimeout,

    /// <summary>The store refused the request (HTTP 403, usually an anti-bot system).</summary>
    StoreBlocked,

    /// <summary>The store asked us to slow down (HTTP 429).</summary>
    StoreRateLimited,

    /// <summary>The store had an internal error (HTTP 5xx).</summary>
    StoreServerError,

    /// <summary>The request was skipped because the host is paused (throttling / Retry-After).</summary>
    StoreThrottled,

    /// <summary>The outbound proxy is unreachable.</summary>
    ProxyUnavailable,

    /// <summary>The proxy could not open a connection to the store.</summary>
    ProxyTunnelFailed,

    /// <summary>The store's address could not be resolved.</summary>
    DnsFailed,

    /// <summary>The store refused the connection.</summary>
    ConnectionRefused,

    /// <summary>A provider needs credentials/a feed that are not configured.</summary>
    StoreIntegrationNotConfigured,

    /// <summary>A provider rejected the configured credentials.</summary>
    StoreIntegrationUnauthorized,

    /// <summary>A provider's API/feed answered with an error.</summary>
    StoreIntegrationApiError,

    /// <summary>A notification channel is turned off.</summary>
    NotificationChannelDisabled,

    /// <summary>A notification could not be sent.</summary>
    NotificationFailed,
}
