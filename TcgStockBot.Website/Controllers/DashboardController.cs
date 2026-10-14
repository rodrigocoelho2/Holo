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
/// <param name="options">The sign-in settings (whether anyone may create an account).</param>
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
            return View("Landing", new LandingModel { CanCreateAccount = options.Value.AllowRegistration });
        }

        return View(new DashboardModel { UserName = User.Identity.Name ?? string.Empty, IsAdmin = User.IsInRole("Admin") });
    }

    /// <summary>
    /// The public page that explains the app (also reachable when signed in).
    /// </summary>
    /// <returns>The view.</returns>
    [HttpGet("/about")]
    [AllowAnonymous]
    public IActionResult Landing() => View("Landing", new LandingModel
    {
        SignedIn = User.Identity?.IsAuthenticated == true,
        CanCreateAccount = options.Value.AllowRegistration,
    });

    /// <summary>Old address of the public page.</summary>
    /// <returns>A redirect.</returns>
    [HttpGet("/sobre")]
    [AllowAnonymous]
    public IActionResult OldLanding() => RedirectPermanent("/about");
}
