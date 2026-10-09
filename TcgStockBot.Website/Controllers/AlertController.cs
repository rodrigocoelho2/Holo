using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model.Alerts;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Alert actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class AlertController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Lists the alert history.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new AlertModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Forgets every alerted product.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAsync()
    {
        var model = new AlertModel(ApiClient);
        return Json(await model.ResetAsync());
    }

    /// <summary>Empties the alert list.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAsync()
    {
        var model = new AlertModel(ApiClient);
        return Json(await model.ClearAsync());
    }
}
