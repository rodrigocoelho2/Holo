using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters;

/// <summary>
/// Reads a server-rendered search results page with CSS selectors. Works only for stores whose results are
/// in the HTML (not loaded by JavaScript) and that don't serve a bot challenge.
/// </summary>
/// <param name="httpClientFactory">Creates the store HttpClient.</param>
internal sealed class HtmlStoreAdapter(IHttpClientFactory httpClientFactory) : IStoreAdapter
{
    /// <inheritdoc />
    public StorePlatform Platform => StorePlatform.Html;

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken)
    {
        var selectors = store.Html
            ?? throw new InvalidOperationException($"Store '{store.Name}' uses the Html platform but has no selectors.");

        var searchUri = new Uri(selectors.SearchUrlTemplate.Replace("{query}", Uri.EscapeDataString(query), StringComparison.Ordinal));

        using var response = await httpClientFactory.CreateClient(StoreHttpClient.Name).GetAsync(searchUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);

        var listings = new List<StoreListing>();
        foreach (var card in document.QuerySelectorAll(selectors.ProductSelector))
        {
            var title = TextOf(card, selectors.TitleSelector);
            var href = LinkOf(card, selectors.LinkSelector);
            if (string.IsNullOrWhiteSpace(title) || href is null)
            {
                continue;
            }

            listings.Add(new StoreListing(
                store.Name,
                title,
                new Uri(searchUri, href),
                PriceParser.Parse(TextOf(card, selectors.PriceSelector)),
                IsInStock(card, selectors),
                ImageUrl: ImageOf(card, searchUri)));
        }

        return listings;
    }

    /// <summary>
    /// The first photo in the product card. Lazy-loading themes keep the real URL in data-src (and similar)
    /// and put a placeholder in src, so those are read first.
    /// </summary>
    private static string? ImageOf(IElement card, Uri pageUri)
    {
        foreach (var image in card.QuerySelectorAll("img"))
        {
            foreach (var attribute in (string[])["data-src", "data-lazy-src", "data-original", "src"])
            {
                if (StoreImageUrl.Normalize(image.GetAttribute(attribute), pageUri) is { } url)
                {
                    return url;
                }
            }
        }

        return null;
    }

    private static string TextOf(IElement card, string selector)
    {
        var element = string.IsNullOrWhiteSpace(selector) ? card : card.QuerySelector(selector);
        return element?.TextContent.Trim() ?? string.Empty;
    }

    private static string? LinkOf(IElement card, string selector)
    {
        var link = string.IsNullOrWhiteSpace(selector)
            ? card.QuerySelector("a[href]") ?? (card.HasAttribute("href") ? card : null)
            : card.QuerySelector(selector);

        return link?.GetAttribute("href");
    }

    /// <summary>An out-of-stock marker wins; otherwise an in-stock marker is required when one is configured.</summary>
    private static bool IsInStock(IElement card, StoreHtmlSelector selectors)
    {
        if (!string.IsNullOrWhiteSpace(selectors.OutOfStockSelector) && card.QuerySelector(selectors.OutOfStockSelector) is not null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(selectors.InStockSelector) || card.QuerySelector(selectors.InStockSelector) is not null;
    }
}
