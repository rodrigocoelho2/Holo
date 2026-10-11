using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace TcgStockBot.Website.Security;

/// <summary>
/// Sign-in with a login form and a cookie. With no password configured the dashboard is open: every request is
/// treated as signed in.
/// </summary>
internal static class DashboardAuthentication
{
    /// <summary>The login page.</summary>
    public const string LoginPath = "/entrar";

    /// <summary>The name of the session cookie.</summary>
    public const string CookieName = "cardforge.session";

    /// <summary>The authentication type of the open (no password) mode.</summary>
    public const string OpenAuthenticationType = "open";

    /// <summary>
    /// Registers cookie authentication, the login throttle and (optionally) persistent cookie keys.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDashboardAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>() ?? new DashboardOptions();

        var dataProtection = services.AddDataProtection().SetApplicationName("CardForge");
        if (!string.IsNullOrWhiteSpace(options.KeysDirectory))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(options.KeysDirectory));
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginThrottle>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = CookieName;
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // the NAS is usually plain HTTP on the LAN
                cookie.LoginPath = LoginPath;
                cookie.ReturnUrlParameter = "volta";
                cookie.SlidingExpiration = true;
                cookie.ExpireTimeSpan = TimeSpan.FromDays(Math.Clamp(options.RememberDays, 1, 365));
                cookie.Events.OnRedirectToLogin = context =>
                {
                    // Pages go to the login form; the dashboard's JSON calls get a plain 401 (the script then opens the form).
                    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.Redirect(context.RedirectUri);
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Adds authentication to the pipeline; in open mode every request is signed in.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseDashboardAuthentication(this WebApplication app)
    {
        app.UseAuthentication();

        if (!app.Services.GetRequiredService<IOptions<DashboardOptions>>().Value.RequiresPassword)
        {
            var open = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Card Forge")], OpenAuthenticationType));
            app.Use((context, next) =>
            {
                context.User = open;
                return next(context);
            });
        }

        app.UseAuthorization();
        return app;
    }

    /// <summary>
    /// Signs the browser in.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="remember">Keep the session after the browser closes.</param>
    /// <param name="rememberDays">For how long, when remembered.</param>
    /// <returns>A task that completes when the cookie is set.</returns>
    public static Task SignInAsync(HttpContext context, bool remember, int rememberDays)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Card Forge")], CookieAuthenticationDefaults.AuthenticationScheme));
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(Math.Clamp(rememberDays, 1, 365)) : null,
            AllowRefresh = true,
        });
    }
}
