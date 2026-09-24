using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model.Dashboard;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// The dashboard page.
/// </summary>
/// <param name="apiClient">The API client.</param>
public partial class DashboardController(TcgStockBotClient apiClient) : WebsiteBaseController(apiClient)
{
    /// <summary>
    /// Shows the dashboard shell; its data is loaded by the JSON actions of the other controllers.
    /// </summary>
    /// <returns>The view.</returns>
    [HttpGet]
    public IActionResult Index() => View(new DashboardModel());
}
