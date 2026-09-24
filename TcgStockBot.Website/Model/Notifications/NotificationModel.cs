using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Notifications;

/// <summary>
/// Notification-channel operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class NotificationModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Gets the state of each channel.</summary>
    /// <returns>The channel states.</returns>
    public Task<AjaxDataResult<NotificationDTO>> GetAsync() => CallAsync(() => ApiClient.NotificationGetAsync());

    /// <summary>Sends a test alert through one channel.</summary>
    /// <param name="item">The channel.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> TestAsync(NotificationTestRequest item) => CallAsync(() => ApiClient.NotificationTestAsync(item));
}
