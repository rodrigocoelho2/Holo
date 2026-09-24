using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model.Monitors;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Monitor actions of the dashboard (live state, check now, pause, resume).
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class MonitorController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>Gets the live state.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var model = new MonitorModel(ApiClient);
        return Json(await model.GetAsync());
    }

    /// <summary>Asks for a check now.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAsync()
    {
        var model = new MonitorModel(ApiClient);
        return Json(await model.RunAsync());
    }

    /// <summary>Pauses the bot.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PauseAsync()
    {
        var model = new MonitorModel(ApiClient);
        return Json(await model.PauseAsync());
    }

    /// <summary>Resumes the bot.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeAsync()
    {
        var model = new MonitorModel(ApiClient);
        return Json(await model.ResumeAsync());
    }
}
