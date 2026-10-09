using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.Stores;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Store actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class StoreController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Lists every store.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Creates, replaces or renames a store.</summary>
    /// <param name="item">The store data.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAsync([FromBody] StoreSaveRequest item)
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.SaveAsync(item));
    }

    /// <summary>Deletes a store.</summary>
    /// <param name="identifier">The store name.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAsync([FromQuery] string identifier)
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.DeleteAsync(identifier));
    }

    /// <summary>Turns a store on or off.</summary>
    /// <param name="item">What to change.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAsync([FromBody] StoreEnableRequest item)
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.EnableAsync(item));
    }

    /// <summary>Runs one search on a store configuration.</summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProbeAsync([FromBody] StoreProbeRequest item)
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.ProbeAsync(item));
    }

    /// <summary>Detects how a store can be searched from its address.</summary>
    /// <param name="item">The store address.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DetectAsync([FromBody] StoreDetectRequest item)
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.DetectAsync(item));
    }

    /// <summary>Tests one saved store and keeps the result on it.</summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestAsync([FromBody] StoreTestRequest item)
    {
        var model = new StoreModel(ApiClient);
        return Json(await model.TestAsync(item));
    }
}
