using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TcgStockBot.Website.Model.Account;
using TcgStockBot.Website.Security;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Login form and sign-out.
/// </summary>
/// <param name="options">Dashboard settings (password).</param>
/// <param name="throttle">Slows down password guessing.</param>
/// <param name="logger">Logger (never logs the password).</param>
[AllowAnonymous]
public sealed class AccountController(IOptions<DashboardOptions> options, LoginThrottle throttle, ILogger<AccountController> logger) : Controller
{
    private static readonly TimeSpan FailureDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>
    /// Shows the login form (or goes straight to the dashboard when there is no password or already signed in).
    /// </summary>
    /// <param name="volta">Where to go after signing in.</param>
    /// <returns>The view or a redirect.</returns>
    [HttpGet("/entrar")]
    public IActionResult Login(string? volta = null)
    {
        if (!options.Value.RequiresPassword || User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(volta));
        }

        return View(new LoginModel { ReturnUrl = SafeReturnUrl(volta), RememberDays = options.Value.RememberDays });
    }

    /// <summary>
    /// Checks the password and signs the browser in.
    /// </summary>
    /// <param name="password">The password typed.</param>
    /// <param name="remember">Keep the session on this device.</param>
    /// <param name="volta">Where to go after signing in.</param>
    /// <returns>A redirect, or the form again with the error.</returns>
    [HttpPost("/entrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginAsync([FromForm] string? password, [FromForm] bool remember, [FromForm] string? volta)
    {
        var settings = options.Value;
        var returnUrl = SafeReturnUrl(volta);
        if (!settings.RequiresPassword)
        {
            return LocalRedirect(returnUrl);
        }

        var address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var model = new LoginModel { ReturnUrl = returnUrl, Remember = remember, RememberDays = settings.RememberDays };

        var wait = throttle.RetryAfter(address);
        if (wait > TimeSpan.Zero)
        {
            model.Error = $"Demasiadas tentativas erradas. Tenta outra vez daqui a {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} min.";
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return View("Login", model);
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password ?? string.Empty), Encoding.UTF8.GetBytes(settings.Password)))
        {
            throttle.Failed(address);
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Wrong dashboard password from {Address}", address);

            await Task.Delay(FailureDelay);
            model.Error = "Password errada.";
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return View("Login", model);
        }

        throttle.Succeeded(address);
        await DashboardAuthentication.SignInAsync(HttpContext, remember, settings.RememberDays);
        return LocalRedirect(returnUrl);
    }

    /// <summary>
    /// Signs the browser out and shows the public page.
    /// </summary>
    /// <returns>A redirect to the start.</returns>
    [HttpPost("/sair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }

    private string SafeReturnUrl(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) && !url.StartsWith("/entrar", StringComparison.OrdinalIgnoreCase) ? url : "/";
}
