using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;
using TcgStockBot.Website.Model.StockTasks;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Stock task actions of the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class StockTaskController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Lists every task.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync()
    {
        var model = new StockTaskModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Creates or replaces a task.</summary>
    /// <param name="item">The task data.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAsync([FromBody] StockTaskSaveRequest item)
    {
        var model = new StockTaskModel(ApiClient);
        return Json(await model.SaveAsync(item));
    }

    /// <summary>Deletes a task.</summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAsync([FromQuery] string identifier)
    {
        var model = new StockTaskModel(ApiClient);
        return Json(await model.DeleteAsync(identifier));
    }

    /// <summary>Starts or stops one task, or every task.</summary>
    /// <param name="item">What to change.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableAsync([FromBody] StockTaskEnableRequest item)
    {
        var model = new StockTaskModel(ApiClient);
        return Json(await model.EnableAsync(item));
    }
}
