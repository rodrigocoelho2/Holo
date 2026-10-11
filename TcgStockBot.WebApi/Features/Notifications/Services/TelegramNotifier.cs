using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Models;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Sends alerts to Telegram through the Bot API (sendMessage, HTML formatting). The bot token is part of the URL, so the
/// HttpClient loggers are removed for this client.
/// </summary>
/// <param name="options">Telegram settings.</param>
/// <param name="httpClientFactory">Creates the HTTP client.</param>
/// <param name="logger">Logger.</param>
internal sealed class TelegramNotifier(
    IOptions<TelegramOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<TelegramNotifier> logger) : INotifier
{
    /// <summary>The named HttpClient.</summary>
    public const string HttpClientName = "telegram";

    /// <inheritdoc />
    public NotificationChannel? Channel => NotificationChannel.Telegram;

    /// <inheritdoc />
    public bool IsEnabled => options.Value.Enabled;

    /// <inheritdoc />
    public async Task SendAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await SendTextAsync(settings, AlertMessageFormatter.Telegram(alerts, settings.MaxAlertsPerMessage), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Alert Telegram sent ({Count} product(s))", alerts.Count);
    }

    /// <inheritdoc />
    public async Task SendNoticeAsync(string subject, string text, CancellationToken cancellationToken)
    {
        await SendTextAsync(options.Value, AlertMessageFormatter.TelegramNotice(subject, text), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Notice Telegram sent");
    }

    private async Task SendTextAsync(TelegramOptions settings, string html, CancellationToken cancellationToken)
    {
        var body = new { chat_id = settings.ChatId.Trim(), text = html, parse_mode = "HTML", disable_web_page_preview = false };
        using var response = await httpClientFactory.CreateClient(HttpClientName).PostAsJsonAsync(BuildRequestUri(settings), body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // The Bot API explains the problem in "description" (wrong chat id, bot not started…); never echo the URL.
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Telegram answered HTTP {(int)response.StatusCode}: {Trim(detail)}");
        }
    }

    /// <summary>The sendMessage URL of the bot.</summary>
    /// <param name="settings">Telegram settings.</param>
    /// <returns>The URL.</returns>
    internal static Uri BuildRequestUri(TelegramOptions settings) =>
        new($"{settings.ApiUrl.TrimEnd('/')}/bot{settings.BotToken.Trim()}/sendMessage");

    private static string Trim(string text) => text.Length <= 300 ? text : text[..300];
}
