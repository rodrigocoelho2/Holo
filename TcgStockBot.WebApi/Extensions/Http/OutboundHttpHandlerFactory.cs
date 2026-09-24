using System.Net;

namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// The outbound route actually in use, decided once from <see cref="OutboundHttpOptions"/>.
/// </summary>
/// <param name="ViaProxy">Whether requests go through the proxy.</param>
/// <param name="ProxyAuthority">host:port of the proxy, when used.</param>
public sealed record OutboundRoute(bool ViaProxy, string? ProxyAuthority)
{
    /// <summary>Gets a short label for logs: "proxy price-proxy:3128" or "direct".</summary>
    public string Label => ViaProxy ? $"proxy {ProxyAuthority}" : "direct";
}

/// <summary>
/// Builds the primary handler of the outbound HttpClients: through the configured HTTP proxy, or direct.
/// IHttpClientFactory owns the handler's lifetime, so connections are pooled and rotated as usual.
/// </summary>
internal static class OutboundHttpHandlerFactory
{
    /// <summary>
    /// Resolves the route. An enabled proxy with an invalid URL falls back to direct and logs an error,
    /// instead of failing every request or the whole application.
    /// </summary>
    /// <param name="options">The outbound settings.</param>
    /// <param name="logger">Logger for configuration problems.</param>
    /// <param name="proxy">The proxy to use (null when direct).</param>
    /// <returns>The route.</returns>
    public static OutboundRoute ResolveRoute(OutboundHttpOptions options, ILogger logger, out WebProxy? proxy)
    {
        proxy = null;
        if (!options.Proxy.Enabled)
        {
            return new OutboundRoute(false, null);
        }

        if (!Uri.TryCreate(options.Proxy.Url, UriKind.Absolute, out var proxyUri)
            || proxyUri.Scheme != Uri.UriSchemeHttp
            || string.IsNullOrEmpty(proxyUri.Host))
        {
            logger.LogError("OutboundHttp:Proxy:Enabled is true but OutboundHttp:Proxy:Url is not a valid http:// URL; provider requests go DIRECT");
            return new OutboundRoute(false, null);
        }

        proxy = new WebProxy(new UriBuilder(proxyUri) { UserName = string.Empty, Password = string.Empty }.Uri)
        {
            BypassProxyOnLocal = false,
        };

        if (!string.IsNullOrEmpty(proxyUri.UserInfo))
        {
            var parts = proxyUri.UserInfo.Split(':', 2);
            proxy.Credentials = new NetworkCredential(Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
        }

        return new OutboundRoute(true, $"{proxyUri.Host}:{proxyUri.Port}");
    }

    /// <summary>
    /// Creates the primary handler for one outbound HttpClient.
    /// </summary>
    /// <param name="options">The outbound settings.</param>
    /// <param name="logger">Logger for configuration problems.</param>
    /// <returns>A SocketsHttpHandler that uses the proxy, or explicitly none (UseProxy = false, so HTTP_PROXY env vars are ignored too).</returns>
    public static SocketsHttpHandler Create(OutboundHttpOptions options, ILogger logger)
    {
        var route = ResolveRoute(options, logger, out var proxy);

        return new SocketsHttpHandler
        {
            UseProxy = route.ViaProxy,
            Proxy = proxy,
            ConnectTimeout = options.ConnectTimeout > TimeSpan.Zero ? options.ConnectTimeout : TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.None,
        };
    }
}
