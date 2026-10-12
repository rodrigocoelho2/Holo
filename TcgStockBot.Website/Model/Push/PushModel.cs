using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Push;

/// <summary>
/// Push notification operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class PushModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Gets the push state.</summary>
    /// <returns>The state.</returns>
    public Task<AjaxDataResult<PushDTO>> GetAsync() => CallAsync(() => ApiClient.PushGetAsync());

    /// <summary>Adds this browser for the signed-in account.</summary>
    /// <param name="item">The browser's subscription.</param>
    /// <param name="userName">The signed-in account.</param>
    /// <returns>The device identifier.</returns>
    public Task<AjaxDataResult<PushSubscribeResponse>> SubscribeAsync(PushSubscribeRequest item, string userName)
    {
        item.UserName = userName;
        return CallAsync(() => ApiClient.PushSubscribeAsync(item));
    }

    /// <summary>Removes a device.</summary>
    /// <param name="item">The device.</param>
    /// <returns>Success.</returns>
    public Task<AjaxDataResult<bool>> UnsubscribeAsync(PushUnsubscribeRequest item) => CallAsync(() => ApiClient.PushUnsubscribeAsync(item));
}
