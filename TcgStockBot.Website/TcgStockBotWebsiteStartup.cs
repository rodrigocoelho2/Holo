using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Security;

namespace TcgStockBot.Website;

/// <summary>
/// Service registration and request pipeline of the Website.
/// </summary>
internal static class TcgStockBotWebsiteStartup
{
    /// <summary>
    /// Registers MVC, anti-forgery, the login (cookie) and the WebApi client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTcgStockBotWebsite(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DashboardOptions>(configuration.GetSection(DashboardOptions.SectionName));
        services.AddDashboardAuthentication(configuration);

        services.AddControllersWithViews()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // The dashboard script sends the token in this header on every POST.
        services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

        services.AddTcgStockBotClient(configuration);

        // Behind a reverse proxy (Nginx Proxy Manager, Cloudflare) the request arrives as plain HTTP: take the real scheme
        // and client from X-Forwarded-Proto / X-Forwarded-For, so links and the login cookie are HTTPS. The proxy runs in
        // another container, so its address is not known in advance; only the last hop is read.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseTcgStockBotWebsite(this WebApplication app)
    {
        app.UseForwardedHeaders();

        var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        // Static files are public (the landing page, the login form and the app icons need them).
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            // Revalidate on every load so a new version shows up right after a redeploy.
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
        });

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}");

        return app;
    }
}
