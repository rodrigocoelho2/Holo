using System.Globalization;
using System.Text;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models.Entities;
using TcgStockBot.WebApi.Features.Alerts.Repositories;
using TcgStockBot.WebApi.Features.Monitors.Services;
using TcgStockBot.WebApi.Features.Settings.Repositories;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Once a day, at the hour set in Definições, e-mails a summary: the alerts of the last 24 hours, what is in stock at
/// your price right now and the stores that are failing. Only by e-mail (the instant channels already got each alert).
/// </summary>
/// <param name="settingRepository">Settings (on/off and hour).</param>
/// <param name="alertRepository">The alert history.</param>
/// <param name="activity">The last cycle's searches.</param>
/// <param name="notifiers">The channels; only e-mail is used.</param>
/// <param name="timeProvider">Clock (local time = the container's TZ, Europe/Lisbon).</param>
/// <param name="logger">Logger.</param>
internal sealed class DailySummaryWorker(
    ISettingRepository settingRepository,
    IAlertRepository alertRepository,
    BotActivity activity,
    IEnumerable<INotifier> notifiers,
    TimeProvider timeProvider,
    ILogger<DailySummaryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(5);
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-IE");
    private DateOnly? _lastSent;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendIfDueAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Daily summary failed");
                activity.Log("error", $"The daily summary failed: {exception.Message}");
            }

            try
            {
                await Task.Delay(CheckEvery, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Sends the summary when it is on, it is the chosen hour and it was not sent today yet.</summary>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>True when a summary was sent.</returns>
    internal async Task<bool> SendIfDueAsync(CancellationToken cancellationToken)
    {
        var settings = await settingRepository.GetAsync();
        var now = timeProvider.GetLocalNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        if (!settings.DailySummaryEnabled || now.Hour != settings.DailySummaryHour || _lastSent == today)
        {
            return false;
        }

        var channels = notifiers.Where(static notifier => notifier.Channel is not null && notifier.IsEnabled).ToList();
        _lastSent = today;
        if (channels.Count == 0)
        {
            activity.Log("warn", "The daily summary is on, but no alert channel (email, Telegram, WhatsApp) is turned on in .env.");
            return false;
        }

        var history = await alertRepository.ListHistoryAsync();
        var (subject, text) = Build(history, activity.Snapshot(settings.Paused), now);
        var sent = new List<string>();
        foreach (var channel in channels)
        {
            try
            {
                await channel.SendNoticeAsync(subject, text, cancellationToken);
                sent.Add(channel.Channel!.Value.ToString());
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                activity.Log("error", $"The daily summary by {channel.Channel} failed: {exception.Message}");
            }
        }

        if (sent.Count > 0)
        {
            activity.Log("info", $"Daily summary sent by {string.Join(", ", sent)}.");
        }

        return sent.Count > 0;
    }

    /// <summary>The subject and text of the summary.</summary>
    /// <param name="history">Every alert sent.</param>
    /// <param name="monitor">The current monitor state (last cycle's searches).</param>
    /// <param name="now">Now.</param>
    /// <returns>Subject and plain text.</returns>
    internal static (string Subject, string Text) Build(IReadOnlyList<AlertHistory> history, Monitors.Models.DTOs.MonitorDTO monitor, DateTimeOffset now)
    {
        var lastDay = history.Where(alert => alert.At >= now.AddDays(-1)).OrderByDescending(static alert => alert.At).ToList();
        var offers = monitor.Searches
            .SelectMany(static search => search.Listings.Where(static listing => listing.Outcome.IsMatch()).Select(listing => (search.StoreName, Listing: listing)))
            .DistinctBy(static offer => offer.Listing.Url)
            .OrderBy(static offer => offer.Listing.Price)
            .ToList();
        var failing = monitor.Searches.Where(static search => !search.Succeeded).Select(static search => search.StoreName).Distinct().Order().ToList();

        var text = new StringBuilder();
        text.AppendLine($"Summary for {now.ToString("dddd, d MMMM", English)}.").AppendLine();

        text.AppendLine(lastDay.Count == 0 ? "No alerts in the last 24 hours." : $"Alerts in the last 24 hours: {lastDay.Count}");
        foreach (var alert in lastDay.Take(15))
        {
            text.AppendLine($"• {alert.At.ToLocalTime():HH:mm} · {alert.Title} — {alert.StoreName} · {Price(alert.Price)}");
        }

        text.AppendLine().AppendLine(offers.Count == 0 ? "Nothing in stock at your price right now." : $"In stock at your price right now: {offers.Count}");
        foreach (var (storeName, listing) in offers.Take(15))
        {
            text.AppendLine($"• {listing.Title} — {storeName} · {Price(listing.Price)}").AppendLine($"  {listing.Url}");
        }

        if (failing.Count > 0)
        {
            text.AppendLine().AppendLine($"Stores with errors in the last cycle: {string.Join(", ", failing)}");
        }

        var subject = $"[Card Forge] Daily summary: {lastDay.Count} alert(s), {offers.Count} in stock";
        return (subject, text.ToString().TrimEnd());
    }

    private static string Price(decimal? price) => price?.ToString("C", English) ?? "no price";
}
