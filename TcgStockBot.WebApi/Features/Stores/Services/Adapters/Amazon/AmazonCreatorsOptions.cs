namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Amazon;

/// <summary>
/// Amazon Creators API settings, bound from the "Amazon" section (in Docker: Amazon__CredentialId, …).
/// Credentials come from Associates Central → Creators API; they are never stored in the source code.
/// </summary>
public sealed class AmazonCreatorsOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Amazon";

    /// <summary>Gets or sets the Credential ID (OAuth client_id).</summary>
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Credential Secret (OAuth client_secret). Secret: .env / user-secrets only.</summary>
    public string CredentialSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the credential version shown next to the credential ("3.2" = EU, which covers amazon.es).</summary>
    public string CredentialVersion { get; set; } = "3.2";

    /// <summary>Gets or sets the Associates partner (tracking) tag, e.g. "mytag-21".</summary>
    public string PartnerTag { get; set; } = string.Empty;

    /// <summary>Gets or sets the marketplace host.</summary>
    public string Marketplace { get; set; } = "www.amazon.es";

    /// <summary>Gets or sets the API base URL.</summary>
    public string ApiBaseUrl { get; set; } = "https://creatorsapi.amazon";

    /// <summary>Gets or sets an explicit token endpoint; empty = derived from <see cref="CredentialVersion"/>.</summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the mandatory values are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CredentialId) && !string.IsNullOrWhiteSpace(CredentialSecret) && !string.IsNullOrWhiteSpace(PartnerTag);

    /// <summary>
    /// The token endpoint for the credential version (3.x credentials use Login with Amazon endpoints per region).
    /// </summary>
    /// <returns>The endpoint, or null when the version is not supported.</returns>
    public Uri? ResolveTokenEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(TokenEndpoint))
        {
            return Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out var explicitEndpoint) ? explicitEndpoint : null;
        }

        return CredentialVersion.Trim() switch
        {
            "3.1" => new Uri("https://api.amazon.com/auth/o2/token"),
            "3.2" => new Uri("https://api.amazon.co.uk/auth/o2/token"),
            "3.3" => new Uri("https://api.amazon.co.jp/auth/o2/token"),
            _ => null,
        };
    }
}
