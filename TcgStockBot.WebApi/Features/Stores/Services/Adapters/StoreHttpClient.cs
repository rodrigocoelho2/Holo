using TcgStockBot.WebApi.Extensions.Data;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// The named HttpClient used for every store request.
/// </summary>
internal static class StoreHttpClient
{
    /// <summary>The client name registered with IHttpClientFactory.</summary>
    public const string Name = "stores";
}

/// <summary>
/// Stamps every store request with the User-Agent from the live settings (editable in the dashboard).
/// </summary>
/// <param name="dataContext">The data context holding the settings.</param>
internal sealed class UserAgentHandler(BotDataContext dataContext) : DelegatingHandler
{
    /// <summary>
    /// Sets the User-Agent and forwards the request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.UserAgent.Clear();
        request.Headers.TryAddWithoutValidation("User-Agent", dataContext.Snapshot().UserAgent);
        return base.SendAsync(request, cancellationToken);
    }
}
