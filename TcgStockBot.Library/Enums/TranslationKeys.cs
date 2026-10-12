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

    /// <summary>A store has no integration (Manual platform): it is never searched.</summary>
    StoreWithoutIntegration,

    /// <summary>An unknown kind of product code.</summary>
    UnknownProductKind,

    /// <summary>The MSRP tolerance is outside the allowed range.</summary>
    MsrpToleranceRange,

    /// <summary>An hour of the day outside 0–23.</summary>
    HourRange,

    /// <summary>A key that may only contain letters, digits and '_'.</summary>
    InvalidKey,

    /// <summary>Store detection found a platform.</summary>
    StoreDetected,

    /// <summary>Store detection was refused by the site.</summary>
    StoreDetectBlocked,

    /// <summary>Store detection could not reach the site.</summary>
    StoreDetectUnreachable,

    /// <summary>Store detection found no searchable platform.</summary>
    StoreDetectUnknown,

    /// <summary>The subject of the "stores turned off" notice.</summary>
    StoresAutoDisabledSubject,

    /// <summary>One line of the "stores turned off" notice.</summary>
    StoreAutoDisabledLine,

    /// <summary>Pre-orders open after the release date.</summary>
    PreOrderAfterRelease,

    /// <summary>A user name with characters or a length that is not allowed.</summary>
    UserNameInvalid,

    /// <summary>A password shorter than the minimum.</summary>
    PasswordTooShort,

    /// <summary>Wrong user name or password (never says which).</summary>
    WrongCredentials,

    /// <summary>The current password is wrong.</summary>
    PasswordWrong,

    /// <summary>The last admin account cannot be deleted.</summary>
    LastAdmin,

    /// <summary>A shipping amount outside 0–500 €.</summary>
    ShippingRange,

    /// <summary>A push endpoint that is not a browser push service.</summary>
    PushEndpointNotAllowed,

    /// <summary>Push subscription keys that are not a P-256 key and a 16-byte secret.</summary>
    PushKeysInvalid,
}
