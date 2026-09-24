using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TcgStockBot.Library.Constants;

namespace TcgStockBot.Sdk;

/// <summary>
/// Where the WebApi is and the key to call it, bound from the "TcgStockBotApi" section.
/// </summary>
public sealed class TcgStockBotClientOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "TcgStockBotApi";

    /// <summary>Gets or sets the WebApi base URL, e.g. http://webapi:8080/.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the key sent in x-api-key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the request timeout (store probes can take a while).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// Registers <see cref="TcgStockBotClient"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed client with its base address and x-api-key header.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the "TcgStockBotApi" section.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTcgStockBotClient(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(TcgStockBotClientOptions.SectionName).Get<TcgStockBotClientOptions>() ?? new TcgStockBotClientOptions();
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException("TcgStockBotApi:Url must be an absolute URL (env TcgStockBotApi__Url).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("TcgStockBotApi:ApiKey is required (env TcgStockBotApi__ApiKey).");
        }

        // A trailing slash makes relative paths ("stocktask/list") append to the base path instead of replacing it.
        var normalizedBase = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");

        services.AddHttpClient<TcgStockBotClient>(client =>
        {
            client.BaseAddress = normalizedBase;
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add(ApiHeaders.ApiKey, options.ApiKey);
        });

        return services;
    }
}
