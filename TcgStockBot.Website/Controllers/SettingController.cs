using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.Settings;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Settings actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class SettingController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Gets the settings.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var model = new SettingModel(ApiClient);
        return Json(await model.GetAsync());
    }

    /// <summary>Saves the settings.</summary>
    /// <param name="item">The new settings.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAsync([FromBody] SettingSaveRequest item)
    {
        var model = new SettingModel(ApiClient);
        return Json(await model.SaveAsync(item));
    }
}
