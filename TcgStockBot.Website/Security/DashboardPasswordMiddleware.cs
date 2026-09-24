using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TcgStockBot.Website.Security;

/// <summary>
/// Dashboard settings, bound from the "Dashboard" section (in Docker: Dashboard__Password).
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>Gets or sets the dashboard password (HTTP Basic, any user name). Empty disables the prompt — only acceptable on 127.0.0.1.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Asks for the dashboard password (HTTP Basic) on every request when one is configured.
/// </summary>
internal static class DashboardPasswordMiddleware
{
    /// <summary>
    /// Adds the password check to the pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static IApplicationBuilder UseDashboardPassword(this IApplicationBuilder app)
    {
        var password = app.ApplicationServices.GetRequiredService<IOptions<DashboardOptions>>().Value.Password;
        if (string.IsNullOrEmpty(password))
        {
            return app;
        }

        var expected = Encoding.UTF8.GetBytes(password);

        return app.Use(async (context, next) =>
        {
            if (IsAuthorized(context.Request.Headers.Authorization.ToString(), expected))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"TcgStockBot\", charset=\"UTF-8\"";
        });
    }

    private static bool IsAuthorized(string authorization, byte[] expectedPassword)
    {
        if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authorization["Basic ".Length..].Trim()));
            var separator = credentials.IndexOf(':');
            var password = Encoding.UTF8.GetBytes(separator < 0 ? string.Empty : credentials[(separator + 1)..]);
            return CryptographicOperations.FixedTimeEquals(password, expectedPassword);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
