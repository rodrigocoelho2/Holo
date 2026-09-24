using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Notifications.Services;

/// <summary>
/// Business logic for delivering alerts.
/// </summary>
internal interface INotificationService
{
    /// <summary>
    /// Gets the state of each channel (no secrets).
    /// </summary>
    /// <returns>The channel states.</returns>
    Task<ServiceApiResponse<NotificationDTO>> GetAsync();

    /// <summary>
    /// Sends a sample alert through one channel.
    /// </summary>
    /// <param name="item">The channel to test.</param>
    /// <returns>Success, or the reason it failed.</returns>
    Task<ServiceApiResponse<string>> TestAsync(NotificationTestRequest item);

    /// <summary>
    /// Delivers alerts through every enabled channel. A failing channel never stops the others.
    /// </summary>
    /// <param name="alerts">The new alerts.</param>
    /// <param name="cancellationToken">Cancels delivery.</param>
    /// <returns>A translated message for each channel that failed.</returns>
    Task<IReadOnlyList<string>> NotifyAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken);
}
