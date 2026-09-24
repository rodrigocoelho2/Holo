namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// Why a provider cannot be queried at all. Reported in the dashboard as a clear message, never as a crash.
/// </summary>
public enum StoreIntegrationProblem
{
    /// <summary>The provider needs credentials or a feed that are not configured yet.</summary>
    NotConfigured,

    /// <summary>The provider's API/feed rejected the credentials.</summary>
    Unauthorized,

    /// <summary>The provider's API/feed answered with an error.</summary>
    ApiError,
}

/// <summary>
/// A provider-level failure (missing configuration, rejected credentials, API error), as opposed to a network error.
/// </summary>
/// <param name="provider">The provider name (e.g. "Amazon").</param>
/// <param name="problem">The kind of problem.</param>
/// <param name="detail">A short, safe explanation (never a secret).</param>
public sealed class StoreIntegrationException(string provider, StoreIntegrationProblem problem, string detail)
    : Exception($"{provider}: {detail}")
{
    /// <summary>Gets the provider name.</summary>
    public string Provider { get; } = provider;

    /// <summary>Gets the kind of problem.</summary>
    public StoreIntegrationProblem Problem { get; } = problem;

    /// <summary>Gets the safe explanation.</summary>
    public string Detail { get; } = detail;
}
