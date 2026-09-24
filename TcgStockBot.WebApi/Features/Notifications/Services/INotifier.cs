using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// One way of delivering alerts.
/// </summary>
internal interface INotifier
{
    /// <summary>Gets the channel, or null for the always-on log.</summary>
    NotificationChannel? Channel { get; }

    /// <summary>Gets a value indicating whether the channel is turned on in the configuration.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Delivers alerts.
    /// </summary>
    /// <param name="alerts">The alerts (at least one).</param>
    /// <param name="cancellationToken">Cancels the delivery.</param>
    /// <returns>A task that completes when delivered; throws when the channel fails.</returns>
    Task SendAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken);
}
