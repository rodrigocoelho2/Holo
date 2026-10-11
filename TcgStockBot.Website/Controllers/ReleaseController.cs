using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.Releases;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Launch calendar actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class ReleaseController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Lists every release.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new ReleaseModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Creates or replaces a release.</summary>
    /// <param name="item">The release.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAsync([FromBody] ReleaseSaveRequest item)
    {
        var model = new ReleaseModel(ApiClient);
        return Json(await model.SaveAsync(item));
    }

    /// <summary>Deletes a release.</summary>
    /// <param name="identifier">The release identifier.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAsync([FromQuery] string identifier)
    {
        var model = new ReleaseModel(ApiClient);
        return Json(await model.DeleteAsync(identifier));
    }
}
