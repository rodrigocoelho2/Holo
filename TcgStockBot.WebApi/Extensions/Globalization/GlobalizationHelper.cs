using System.Globalization;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Validation;

namespace TcgStockBot.WebApi.Extensions.Globalization;

/// <summary>
/// Translates message keys and field identifiers into user-facing text.
/// </summary>
internal interface IGlobalizationHelper
{
    /// <summary>
    /// Translates a key.
    /// </summary>
    /// <param name="key">A <see cref="TranslationKeys"/> name or a field identifier.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The translated text, or the key itself when there is no translation.</returns>
    Task<string> TranslateAsync(string key, params object[] arguments);

    /// <summary>
    /// Builds a validation error "{field label}: {message}".
    /// </summary>
    /// <param name="field">The field identifier.</param>
    /// <param name="messageKey">The message key.</param>
    /// <param name="arguments">Values for the message placeholders.</param>
    /// <returns>The validation error.</returns>
    Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments);
}

/// <summary>
/// The user-facing messages (English). The app has one language, so the texts live in code rather than in a
/// translation table.
/// </summary>
internal sealed class GlobalizationHelper : IGlobalizationHelper
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");

    private static readonly Dictionary<string, string> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(TranslationKeys.ValidationProblems)] = "Some of the data sent is not valid.",
        [nameof(TranslationKeys.RequiredField)] = "is required.",
        [nameof(TranslationKeys.GreaterThen0)] = "must be greater than 0.",
        [nameof(TranslationKeys.InvalidUrl)] = "must be a full http(s) address.",
        [nameof(TranslationKeys.InvalidCharacter)] = "cannot contain the '|' character.",
        [nameof(TranslationKeys.AlreadyExists)] = "'{0}' is already taken.",
        [nameof(TranslationKeys.NoRecordsFound)] = "'{0}' does not exist.",
        [nameof(TranslationKeys.PollIntervalTooShort)] = "must be at least 1 minute.",
        [nameof(TranslationKeys.NegativeValue)] = "cannot be negative.",
        [nameof(TranslationKeys.StoreHttpError)] = "The store answered HTTP {0} ({1}).",
        [nameof(TranslationKeys.StoreTimeout)] = "The store did not answer in time.",
        [nameof(TranslationKeys.StoreBlocked)] = "The store refused the request (HTTP 403). Usually its anti-bot protection; the bot does not get around it.",
        [nameof(TranslationKeys.StoreRateLimited)] = "The store asked to slow down (HTTP 429). It is paused and will be checked again later.",
        [nameof(TranslationKeys.StoreServerError)] = "Error on the store's side (HTTP {0}). Trying again next cycle.",
        [nameof(TranslationKeys.StoreThrottled)] = "Request postponed: {0} is paused for {1} more s (request limit).",
        [nameof(TranslationKeys.DnsFailed)] = "Could not resolve the address (DNS).",
        [nameof(TranslationKeys.ConnectionRefused)] = "The connection was refused.",
        [nameof(TranslationKeys.StoreIntegrationNotConfigured)] = "{0}: integration not configured — {1}",
        [nameof(TranslationKeys.StoreIntegrationUnauthorized)] = "{0}: the credentials were refused — {1}",
        [nameof(TranslationKeys.StoreIntegrationApiError)] = "{0}: the API answered with an error — {1}",
        [nameof(TranslationKeys.NotificationChannelDisabled)] = "The {0} channel is turned off in the configuration.",
        [nameof(TranslationKeys.StoreWithoutIntegration)] = "{0} has no integration: it is only listed for reference. Pick a platform (Shopify, WooCommerce, HTML, Awin…) to search it.",
        [nameof(TranslationKeys.UnknownProductKind)] = "the product type '{0}' does not exist.",
        [nameof(TranslationKeys.MsrpToleranceRange)] = "must be between -50% and 300%.",
        [nameof(TranslationKeys.HourRange)] = "must be an hour between 0 and 23.",
        [nameof(TranslationKeys.PreOrderAfterRelease)] = "cannot be after the release date.",
        [nameof(TranslationKeys.InvalidKey)] = "can only have letters, digits and '_'.",
        [nameof(TranslationKeys.StoreDetected)] = "Detected: {0}.",
        [nameof(TranslationKeys.StoreDetectBlocked)] = "The site refused the bot (403/429 or anti-bot protection). It is not bypassed: leave it as \"No integration\", or use an Awin feed/API if there is one.",
        [nameof(TranslationKeys.StoreDetectUnreachable)] = "Could not connect to the site (DNS, connection or timeout).",
        [nameof(TranslationKeys.StoreDetectUnknown)] = "The site answered, but has no recognisable Shopify/WooCommerce API or PrestaShop search. Set up HTML by hand (if the terms allow it) or leave it as \"No integration\".",
        [nameof(TranslationKeys.StoresAutoDisabledSubject)] = "[Holo] Store(s) turned off after errors: {0}",
        [nameof(TranslationKeys.StoreAutoDisabledLine)] = "{0}: {1} cycles in a row with errors — {2}",
        [nameof(TranslationKeys.NotificationFailed)] = "Sending by {0} failed: {1}",
        [nameof(TranslationKeys.UserNameInvalid)] = "must be 3 to 32 letters, digits, '.', '_' or '-'.",
        [nameof(TranslationKeys.PasswordTooShort)] = "must have at least {0} characters.",
        [nameof(TranslationKeys.WrongCredentials)] = "Wrong user name or password.",
        [nameof(TranslationKeys.PasswordWrong)] = "is wrong.",
        [nameof(TranslationKeys.LastAdmin)] = "The last admin account cannot be deleted.",
        [nameof(TranslationKeys.ShippingRange)] = "must be between 0 and 500 €.",
        [nameof(TranslationKeys.PushEndpointNotAllowed)] = "must be the address of a browser push service (Google, Mozilla, Apple or Microsoft).",
        [nameof(TranslationKeys.PushKeysInvalid)] = "are not the keys of a browser push subscription.",
        [nameof(TranslationKeys.DiscordNotLinked)] = "No account here is linked to that Discord account. Sign in with your password and link it in Settings.",
        [nameof(TranslationKeys.DiscordTaken)] = "That Discord account is already linked to another account.",
        [nameof(TranslationKeys.DiscordInvalid)] = "isn't a Discord account.",
        [nameof(TranslationKeys.DiscordNeedsPassword)] = "Discord is the only way into this account. Set a password before unlinking it.",

        // Field labels
        ["Identifier"] = "Id",
        ["DiscordId"] = "Discord account",
        ["Name"] = "Name",
        ["SearchQuery"] = "Search text",
        ["MaxPrice"] = "Max price",
        ["MsrpTolerancePercent"] = "Margin over MSRP",
        ["Kinds"] = "Product types",
        ["DailySummaryHour"] = "Daily summary hour",
        ["ReleaseDate"] = "Release date",
        ["PreOrderDate"] = "Pre-orders open",
        ["Url"] = "Page",
        ["BaseUrl"] = "Base URL",
        ["SearchUrlTemplate"] = "Search URL",
        ["ProductSelector"] = "Product selector",
        ["Query"] = "Search",
        ["PollIntervalSeconds"] = "Interval between checks",
        ["PollJitterSeconds"] = "Random variation",
        ["DelayBetweenRequestsSeconds"] = "Pause between requests",
        ["UserAgent"] = "User-Agent",
        ["Channel"] = "Channel",
        ["UserName"] = "User name",
        ["Password"] = "Password",
        ["CurrentPassword"] = "Current password",
        ["NewPassword"] = "New password",
        ["ShippingCost"] = "Shipping",
        ["FreeShippingFrom"] = "Free shipping from",
        ["Endpoint"] = "Push address",
        ["Keys"] = "Push keys",
    };

    /// <inheritdoc />
    public Task<string> TranslateAsync(string key, params object[] arguments)
    {
        var text = Translations.TryGetValue(key, out var translation) ? translation : key;
        return Task.FromResult(arguments.Length == 0 ? text : string.Format(English, text, arguments));
    }

    /// <inheritdoc />
    public async Task<ApiValidationError> ValidationErrorAsync(string field, string messageKey, params object[] arguments) =>
        new(field, $"{await TranslateAsync(field)}: {await TranslateAsync(messageKey, arguments)}");
}
