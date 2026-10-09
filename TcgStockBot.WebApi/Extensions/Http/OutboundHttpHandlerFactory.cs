using System.Net;

namespace TcgStockBot.WebApi.Extensions.Http;

/// <summary>
/// Builds the primary handler of the outbound HttpClients. IHttpClientFactory owns its lifetime, so connections are
/// pooled and rotated as usual.
/// </summary>
internal static class OutboundHttpHandlerFactory
{
    /// <summary>
    /// Creates the primary handler for one outbound HttpClient.
    /// </summary>
    /// <param name="options">The outbound settings.</param>
    /// <returns>A direct SocketsHttpHandler (UseProxy = false, so HTTP_PROXY environment variables are ignored too).</returns>
    public static SocketsHttpHandler Create(OutboundHttpOptions options) => new()
    {
        UseProxy = false,
        ConnectTimeout = options.ConnectTimeout > TimeSpan.Zero ? options.ConnectTimeout : TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AutomaticDecompression = DecompressionMethods.None,
    };
}
