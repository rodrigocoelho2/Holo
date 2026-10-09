using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model.PriceHistories;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Price history actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class PriceHistoryController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Lists every tracked product.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new PriceHistoryModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Gets the history of one product.</summary>
    /// <param name="identifier">The history key.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync(string identifier)
    {
        var model = new PriceHistoryModel(ApiClient);
        return Json(await model.GetAsync(identifier));
    }
}
