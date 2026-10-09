using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Models;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Sends one WhatsApp message per cycle through the CallMeBot API (GET with phone, text and apikey).
/// CallMeBot only accepts the phone and key in the query string; the request goes over HTTPS and is never logged.
/// </summary>
/// <param name="options">WhatsApp settings.</param>
/// <param name="httpClientFactory">Creates the HttpClient.</param>
/// <param name="logger">Logger.</param>
internal sealed class WhatsAppNotifier(
    IOptions<WhatsAppOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<WhatsAppNotifier> logger) : INotifier
{
    /// <summary>The named HttpClient used for CallMeBot (no request logging).</summary>
    public const string HttpClientName = "callmebot";

    /// <inheritdoc />
    public NotificationChannel? Channel => NotificationChannel.WhatsApp;

    /// <inheritdoc />
    public bool IsEnabled => options.Value.Enabled;

    /// <inheritdoc />
    public async Task SendAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var text = AlertMessageFormatter.WhatsApp(alerts, settings.MaxAlertsPerMessage);
        var requestUri = BuildRequestUri(settings, text);

        using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Alert WhatsApp sent ({Count} product(s))", alerts.Count);
    }

    /// <inheritdoc />
    public async Task SendNoticeAsync(string subject, string text, CancellationToken cancellationToken)
    {
        var requestUri = BuildRequestUri(options.Value, $"⚠️ *{subject}*\n\n{text}");

        using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Notice WhatsApp sent");
    }

    /// <summary>
    /// Builds the CallMeBot request URL.
    /// </summary>
    /// <param name="settings">WhatsApp settings.</param>
    /// <param name="text">The message.</param>
    /// <returns>The request URL.</returns>
    internal static Uri BuildRequestUri(WhatsAppOptions settings, string text)
    {
        var phone = new string(settings.Phone.Where(static character => char.IsDigit(character) || character == '+').ToArray());
        return new Uri($"{settings.ApiUrl}?phone={Uri.EscapeDataString(phone)}&text={Uri.EscapeDataString(text)}&apikey={Uri.EscapeDataString(settings.ApiKey)}");
    }
}
