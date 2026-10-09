namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// Normalizes the product photo URLs the stores return, so the dashboard and alerts always get an absolute
/// http(s) URL or nothing.
/// </summary>
internal static class StoreImageUrl
{
    /// <summary>
    /// Makes a store's image reference absolute: protocol-relative ("//cdn.shopify.com/…") and relative
    /// ("/media/…") URLs are resolved against <paramref name="baseUri"/>; data: URIs, placeholders and other
    /// schemes are dropped.
    /// </summary>
    /// <param name="raw">The value the store returned.</param>
    /// <param name="baseUri">The page or API the value came from.</param>
    /// <returns>The absolute URL, or null when there is no usable image.</returns>
    public static string? Normalize(string? raw, Uri? baseUri = null)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value) || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = "https:" + value;
        }

        Uri? uri;
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.IsFile)
        {
            if (baseUri is null || !Uri.TryCreate(baseUri, value, out uri))
            {
                return null;
            }
        }

        return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp ? uri.AbsoluteUri : null;
    }
}
