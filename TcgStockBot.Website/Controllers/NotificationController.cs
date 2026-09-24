using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.Notifications;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Notification-channel actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class NotificationController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Gets the state of each channel.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var model = new NotificationModel(ApiClient);
        return Json(await model.GetAsync());
    }

    /// <summary>Sends a test alert through one channel.</summary>
    /// <param name="item">The channel.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestAsync([FromBody] NotificationTestRequest item)
    {
        var model = new NotificationModel(ApiClient);
        return Json(await model.TestAsync(item));
    }
}
