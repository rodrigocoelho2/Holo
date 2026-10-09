using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Always on: writes every alert to the log, so nothing is lost if the other channels fail.
/// </summary>
/// <param name="logger">Logger.</param>
internal sealed class LogNotifier(ILogger<LogNotifier> logger) : INotifier
{
    /// <inheritdoc />
    public NotificationChannel? Channel => null;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public Task SendAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            foreach (var alert in alerts)
            {
                logger.LogWarning("IN STOCK: {Task} — {Title} at {Store} for {Price} € → {Url} [LanguageToConfirm={LanguageToConfirm}]",
                    alert.StockTaskName, alert.Listing.Title, alert.Listing.StoreName, alert.Listing.Price, alert.Listing.Url, alert.LanguageToConfirm);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendNoticeAsync(string subject, string text, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("NOTICE: {Subject} — {Text}", subject, text);

        return Task.CompletedTask;
    }
}
