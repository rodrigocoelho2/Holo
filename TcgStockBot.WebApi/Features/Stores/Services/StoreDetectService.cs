using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Detects a store's platform the way you would by hand, with plain GET requests through the normal store client
/// (same User-Agent, same per-domain limits): the home page, then Shopify's /products.json, WooCommerce's Store API
/// and a PrestaShop search page. A 403 / 429 / anti-bot challenge is reported as "blocked" and nothing else is tried —
/// no protection is ever bypassed.
/// </summary>
/// <param name="httpClientFactory">Creates the store HttpClient.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed partial class StoreDetectService(IHttpClientFactory httpClientFactory, IGlobalizationHelper globalizationHelper) : IStoreDetectService
{
    /// <summary>The generic selectors of PrestaShop 1.7+ search results (classic theme and most derived themes).</summary>
    internal static readonly StoreHtmlSelectorDTO PrestaShopSelectors = new()
    {
        ProductSelector = ".product-miniature",
        TitleSelector = ".product-title, .s_title_block",
        PriceSelector = ".price",
        LinkSelector = ".product-title a, .s_title_block a",
        OutOfStockSelector = ".product-flag.out_of_stock, .out-of-stock, .out_of_stock",
        InStockSelector = string.Empty,
    };

    private const string FieldUrl = "Url";
    private const int MaxBodyCharacters = 512 * 1024;

    /// <summary>Collections tried for the Shopify catalog mode, after the one in the address given.</summary>
    private static readonly string[] CollectionHandles = ["pokemon", "pokemon-tcg", "pokemon-tcg-1", "all-pokemon"];

    private static readonly string[] WooCommercePaths = ["/wp-json/wc/store/v1/products", "/wp-json/wc/store/products"];

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(50);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<StoreDetectResult>> DetectAsync(StoreDetectRequest item)
    {
        var text = (item.Url ?? string.Empty).Trim();
        if (text.Length > 0 && !text.Contains("://", StringComparison.Ordinal))
        {
            text = $"https://{text}";   // "store.pt" is what people paste
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            return ServiceApiResponse<StoreDetectResult>
                .ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)))
                .AddErrorRange([await _globalizationHelper.ValidationErrorAsync(FieldUrl, nameof(TranslationKeys.InvalidUrl))]);
        }

        using var timeout = new CancellationTokenSource(Budget);
        var result = await DetectCoreAsync(address, timeout.Token);
        result.Message = result.Status switch
        {
            StoreDetectStatus.Detected => await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreDetected), PlatformName(result)),
            StoreDetectStatus.Blocked => await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreDetectBlocked)),
            StoreDetectStatus.Unreachable => await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreDetectUnreachable)),
            _ => await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreDetectUnknown)),
        };

        return ServiceApiResponse<StoreDetectResult>.Success(result);
    }

    private async Task<StoreDetectResult> DetectCoreAsync(Uri address, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(StoreHttpClient.Name);
        var result = new StoreDetectResult { BaseUrl = Root(address).AbsoluteUri.TrimEnd('/') };

        var home = await GetAsync(client, Root(address), result.Evidence, cancellationToken);
        if (home.Failure is not null)
        {
            result.Status = StoreDetectStatus.Unreachable;
            return result;
        }

        if (IsBlocked(home))
        {
            result.Status = StoreDetectStatus.Blocked;
            return result;
        }

        var root = Root(home.FinalUri);
        result.BaseUrl = root.AbsoluteUri.TrimEnd('/');
        result.SuggestedName = SiteName(home.Body) ?? root.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (await IsShopifyAsync(client, new Uri(root, "/products.json?limit=1"), result.Evidence, cancellationToken))
        {
            result.Status = StoreDetectStatus.Detected;
            result.Platform = StorePlatform.Shopify;
            result.ShopifyCatalogPaths = await FindCollectionAsync(client, root, address, result.Evidence, cancellationToken) ?? string.Empty;
            return result;
        }

        foreach (var path in WooCommercePaths)
        {
            var woo = await GetAsync(client, new Uri(root, $"{path}?per_page=1"), result.Evidence, cancellationToken);
            if (woo.Status == HttpStatusCode.OK && JsonKind(woo.Body) == JsonValueKind.Array)
            {
                result.Status = StoreDetectStatus.Detected;
                result.Platform = StorePlatform.WooCommerce;
                result.WooCommerceProductsPath = path;
                return result;
            }
        }

        var searchTemplate = $"{result.BaseUrl}/index.php?controller=search&s={{query}}";
        var presta = await GetAsync(client, new Uri(searchTemplate.Replace("{query}", "pokemon", StringComparison.Ordinal)), result.Evidence, cancellationToken);
        if (presta.Status == HttpStatusCode.OK && presta.Body.Contains("product-miniature", StringComparison.Ordinal))
        {
            result.Status = StoreDetectStatus.Detected;
            result.Platform = StorePlatform.Html;
            result.Html = new StoreHtmlSelectorDTO
            {
                SearchUrlTemplate = searchTemplate,
                ProductSelector = PrestaShopSelectors.ProductSelector,
                TitleSelector = PrestaShopSelectors.TitleSelector,
                PriceSelector = PrestaShopSelectors.PriceSelector,
                LinkSelector = PrestaShopSelectors.LinkSelector,
                OutOfStockSelector = PrestaShopSelectors.OutOfStockSelector,
                InStockSelector = PrestaShopSelectors.InStockSelector,
            };
            return result;
        }

        result.Status = StoreDetectStatus.Unknown;
        return result;
    }

    /// <summary>The collection for the catalog mode: the one in the address given, else a usual Pokémon handle.</summary>
    private async Task<string?> FindCollectionAsync(HttpClient client, Uri root, Uri address, List<string> evidence, CancellationToken cancellationToken)
    {
        var fromAddress = CollectionHandle().Match(address.AbsolutePath);
        var handles = (fromAddress.Success ? [fromAddress.Groups[1].Value] : Array.Empty<string>())
            .Concat(CollectionHandles)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var handle in handles)
        {
            var response = await GetAsync(client, new Uri(root, $"/collections/{handle}/products.json?limit=1"), evidence, cancellationToken);
            if (response.Status == HttpStatusCode.OK && HasProducts(response.Body))
            {
                return $"/collections/{handle}";
            }

            if (response.Status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests || response.Failure is not null)
            {
                break;   // the store is limiting us: stop asking
            }
        }

        return null;
    }

    private async Task<bool> IsShopifyAsync(HttpClient client, Uri uri, List<string> evidence, CancellationToken cancellationToken)
    {
        var response = await GetAsync(client, uri, evidence, cancellationToken);
        return response.Status == HttpStatusCode.OK && JsonKind(response.Body) == JsonValueKind.Object && HasProperty(response.Body, "products");
    }

    private static async Task<Fetched> GetAsync(HttpClient client, Uri uri, List<string> evidence, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await ReadLimitedAsync(response, cancellationToken);
            evidence.Add($"{uri.PathAndQuery} → HTTP {(int)response.StatusCode}");
            return new Fetched(response.StatusCode, response.RequestMessage?.RequestUri ?? uri, body, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or Extensions.Http.OutboundThrottledException)
        {
            evidence.Add($"{uri.PathAndQuery} → {exception.GetType().Name}");
            return new Fetched(null, uri, string.Empty, exception);
        }
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var buffer = new char[MaxBodyCharacters];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        return new string(buffer, 0, read);
    }

    /// <summary>403 / 429, or an anti-bot challenge page (Cloudflare "Just a moment…").</summary>
    private static bool IsBlocked(Fetched home) =>
        home.Status is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
        || (home.Status is HttpStatusCode.ServiceUnavailable && (home.Body.Contains("challenge-platform", StringComparison.Ordinal) || home.Body.Contains("cf-chl", StringComparison.Ordinal)));

    private static Uri Root(Uri uri) => new($"{uri.Scheme}://{uri.Authority}/");

    private static string PlatformName(StoreDetectResult result) => result.Platform switch
    {
        StorePlatform.Html => "PrestaShop (HTML)",
        StorePlatform.Shopify when result.ShopifyCatalogPaths.Length > 0 => $"Shopify ({result.ShopifyCatalogPaths})",
        _ => result.Platform.ToString(),
    };

    private static JsonValueKind JsonKind(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind;
        }
        catch (JsonException)
        {
            return JsonValueKind.Undefined;
        }
    }

    private static bool HasProperty(string body, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty(name, out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasProducts(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array && products.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>og:site_name, else the first part of the page title ("Store X | Cards" → "Store X").</summary>
    internal static string? SiteName(string html)
    {
        var siteName = OgSiteName().Match(html);
        var name = siteName.Success ? siteName.Groups["name"].Value : TitleTag().Match(html) is { Success: true } title ? TitleSeparator().Split(title.Groups[1].Value)[0] : null;
        name = name is null ? null : WebUtility.HtmlDecode(name).Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name.Length > 60 ? name[..60].Trim() : name;
    }

    [GeneratedRegex(@"/collections/([a-z0-9][a-z0-9\-_]*)", RegexOptions.IgnoreCase)]
    private static partial Regex CollectionHandle();

    [GeneratedRegex(@"<meta[^>]*(?:property|name)\s*=\s*[""']og:site_name[""'][^>]*content\s*=\s*[""'](?<name>[^""']+)[""']|<meta[^>]*content\s*=\s*[""'](?<name>[^""']+)[""'][^>]*(?:property|name)\s*=\s*[""']og:site_name[""']", RegexOptions.IgnoreCase)]
    private static partial Regex OgSiteName();

    [GeneratedRegex(@"<title[^>]*>\s*([^<]+?)\s*</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"\s+[|–—\-·:]\s+")]
    private static partial Regex TitleSeparator();

    private sealed record Fetched(HttpStatusCode? Status, Uri FinalUri, string Body, Exception? Failure);
}
