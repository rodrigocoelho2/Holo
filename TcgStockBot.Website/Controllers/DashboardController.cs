using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model.Dashboard;
using TcgStockBot.Website.Security;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// The dashboard page, and the public page that explains the app.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">Dashboard settings (password).</param>
public partial class DashboardController(TcgStockBotClient apiClient, IOptions<DashboardOptions> options) : WebsiteBaseController(apiClient)
{
    /// <summary>
    /// Signed in: the dashboard shell (its data is loaded by the JSON actions of the other controllers).
    /// Signed out: the public page that explains what the app does, with the way in.
    /// </summary>
    /// <returns>The view.</returns>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return View("Landing", new LandingModel());
        }

        return View(new DashboardModel { CanSignOut = options.Value.RequiresPassword });
    }

    /// <summary>
    /// The public page that explains the app (also reachable when signed in).
    /// </summary>
    /// <returns>The view.</returns>
    [HttpGet("/sobre")]
    [AllowAnonymous]
    public IActionResult Landing() => View("Landing", new LandingModel { SignedIn = User.Identity?.IsAuthenticated == true });
}
