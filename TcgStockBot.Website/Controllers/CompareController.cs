using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.Compares;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Compare actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class CompareController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Lists every compared product.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new CompareModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Gets one compared product.</summary>
    /// <param name="identifier">The group key.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync(string identifier)
    {
        var model = new CompareModel(ApiClient);
        return Json(await model.GetAsync(identifier));
    }

    /// <summary>Corrects the grouping of one listing.</summary>
    /// <param name="item">The listing and its target group.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignAsync([FromBody] CompareAssignRequest item)
    {
        var model = new CompareModel(ApiClient);
        return Json(await model.AssignAsync(item));
    }
}
