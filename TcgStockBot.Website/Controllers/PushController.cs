using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.Push;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Push notification actions of the dashboard: the public key, and turning this browser on or off.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class PushController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Gets the push state and the devices.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var model = new PushModel(ApiClient);
        return Json(await model.GetAsync());
    }

    /// <summary>Adds this browser, for the signed-in account.</summary>
    /// <param name="item">The browser's subscription.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubscribeAsync([FromBody] PushSubscribeRequest item)
    {
        var model = new PushModel(ApiClient);
        return Json(await model.SubscribeAsync(item, User.Identity?.Name ?? string.Empty));
    }

    /// <summary>Removes a device.</summary>
    /// <param name="item">The device.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsubscribeAsync([FromBody] PushUnsubscribeRequest item)
    {
        var model = new PushModel(ApiClient);
        return Json(await model.UnsubscribeAsync(item));
    }
}
