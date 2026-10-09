using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

/// <summary>
/// A store read through its Awin affiliate product feed — the authorized source for retailers that have no public
/// product API. Searching filters the cached feed locally: the retailer's own site is never contacted.
/// Without a configured feed, every search reports "integração não configurada" with <see cref="NotConfiguredHint"/>.
/// </summary>
/// <param name="options">Feed settings.</param>
/// <param name="catalog">Shared feed cache.</param>
internal abstract class AwinFeedStoreAdapter(IOptionsMonitor<AwinFeedOptions> options, AwinFeedCatalog catalog) : IStoreAdapter
{
    private const int MaxResults = 50;

    /// <inheritdoc />
    public abstract StorePlatform Platform { get; }

    /// <summary>Gets the provider name shown in messages and logs.</summary>
    protected abstract string ProviderName { get; }

    /// <summary>Gets what the user must do to get an authorized feed for this store.</summary>
    protected abstract string NotConfiguredHint { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoreListing>> SearchAsync(Store store, string query, CancellationToken cancellationToken)
    {
        var feedKey = FeedKeyOf(store);
        var provider = ProviderNameOf(store);
        if (!options.CurrentValue.Feeds.TryGetValue(feedKey, out var feedUrl) || string.IsNullOrWhiteSpace(feedUrl))
        {
            throw new StoreIntegrationException(provider, StoreIntegrationProblem.NotConfigured, NotConfiguredHintOf(store));
        }

        var products = await catalog.GetProductsAsync(provider, feedKey, feedUrl, cancellationToken);
        return Search(store, products, query);
    }

    /// <summary>The key in AwinFeeds:Feeds of a store (by default the platform name).</summary>
    /// <param name="store">The store.</param>
    /// <returns>The feed key.</returns>
    protected virtual string FeedKeyOf(Store store) => Platform.ToString();

    /// <summary>The provider name shown in messages and logs for a store.</summary>
    /// <param name="store">The store.</param>
    /// <returns>The name.</returns>
    protected virtual string ProviderNameOf(Store store) => ProviderName;

    /// <summary>What the user must do to get an authorized feed for a store.</summary>
    /// <param name="store">The store.</param>
    /// <returns>The hint.</returns>
    protected virtual string NotConfiguredHintOf(Store store) => NotConfiguredHint;

    /// <summary>
    /// Keeps the products whose title contains every word of the search text (accents and case ignored).
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="products">The feed products.</param>
    /// <param name="query">The search text.</param>
    /// <returns>The listings.</returns>
    internal static IReadOnlyList<StoreListing> Search(Store store, IReadOnlyList<AwinFeedProduct> products, string query)
    {
        var words = TextNormalizer.Words(query);

        return products
            .Where(product => words.All(product.NormalizedTitle.Contains))
            .Take(MaxResults)
            .Select(product => new StoreListing(store.Name, product.Title, product.Url, product.Price, product.InStock,
                product.ProductId, product.Currency, product.Seller, product.PreOrder))
            .ToList();
    }
}

/// <summary>
/// FNAC Portugal. FNAC PT has no public product API, and no FNAC Portugal affiliate feed could be verified
/// (the Awin programmes found are FNAC FR/BE/CH/PRO). Its public pages answer 403 to the bot and its terms forbid
/// reproducing the site, so no scraper exists here: this provider only works if you obtain an authorized Awin feed.
/// </summary>
/// <param name="options">Feed settings.</param>
/// <param name="catalog">Shared feed cache.</param>
internal sealed class FnacStoreAdapter(IOptionsMonitor<AwinFeedOptions> options, AwinFeedCatalog catalog) : AwinFeedStoreAdapter(options, catalog)
{
    /// <inheritdoc />
    public override StorePlatform Platform => StorePlatform.Fnac;

    /// <inheritdoc />
    protected override string ProviderName => "FNAC";

    /// <inheritdoc />
    protected override string NotConfiguredHint =>
        "a FNAC Portugal não tem API pública nem um feed de afiliados confirmado, e o site não autoriza recolha automática. "
        + "Só funciona se obtiveres um feed Awin autorizado da FNAC PT: põe o URL do Create-a-Feed em AwinFeeds__Feeds__Fnac.";
}

/// <summary>
/// Worten. No public product API (its Mirakl marketplace API is for sellers only). Worten PT has an Awin programme
/// (advertiser 99897); whether it offers a product feed must be checked after joining.
/// </summary>
/// <param name="options">Feed settings.</param>
/// <param name="catalog">Shared feed cache.</param>
internal sealed class WortenStoreAdapter(IOptionsMonitor<AwinFeedOptions> options, AwinFeedCatalog catalog) : AwinFeedStoreAdapter(options, catalog)
{
    /// <inheritdoc />
    public override StorePlatform Platform => StorePlatform.Worten;

    /// <inheritdoc />
    protected override string ProviderName => "Worten";

    /// <inheritdoc />
    protected override string NotConfiguredHint =>
        "a Worten não tem API pública de produtos. Adere ao programa Awin \"Worten PT\" (99897); se tiver feed de produtos, "
        + "põe o URL do Create-a-Feed em AwinFeeds__Feeds__Worten.";
}

/// <summary>
/// El Corte Inglés (Spain). No public product API (its Mirakl marketplace API is for sellers only). The authorized
/// source is the Awin programme "El Corte Ingles ES" (advertiser 13075), which publishes product feeds.
/// </summary>
/// <param name="options">Feed settings.</param>
/// <param name="catalog">Shared feed cache.</param>
internal sealed class ElCorteInglesStoreAdapter(IOptionsMonitor<AwinFeedOptions> options, AwinFeedCatalog catalog) : AwinFeedStoreAdapter(options, catalog)
{
    /// <inheritdoc />
    public override StorePlatform Platform => StorePlatform.ElCorteIngles;

    /// <inheritdoc />
    protected override string ProviderName => "El Corte Inglés";

    /// <inheritdoc />
    protected override string NotConfiguredHint =>
        "o El Corte Inglés não tem API pública de produtos. Adere ao programa Awin \"El Corte Ingles ES\" (13075) e põe "
        + "o URL do feed gerado no Create-a-Feed em AwinFeeds__Feeds__ElCorteIngles.";
}

/// <summary>
/// Any other retailer with an Awin programme that publishes a product feed (MediaMarkt, FNAC ES, Casa del Libro,
/// Carrefour…). The store's <see cref="Store.AwinFeedKey"/> names the feed: its URL is AwinFeeds:Feeds:{key}
/// (in Docker: AwinFeeds__Feeds__{key}). Only works after the retailer approves you in its Awin programme.
/// </summary>
/// <param name="options">Feed settings.</param>
/// <param name="catalog">Shared feed cache.</param>
internal sealed class GenericAwinStoreAdapter(IOptionsMonitor<AwinFeedOptions> options, AwinFeedCatalog catalog) : AwinFeedStoreAdapter(options, catalog)
{
    /// <inheritdoc />
    public override StorePlatform Platform => StorePlatform.Awin;

    /// <inheritdoc />
    protected override string ProviderName => "Awin";

    /// <inheritdoc />
    protected override string NotConfiguredHint => "falta o URL do feed Awin desta loja.";

    /// <inheritdoc />
    protected override string FeedKeyOf(Store store) => store.AwinFeedKey;

    /// <inheritdoc />
    protected override string ProviderNameOf(Store store) => store.Name;

    /// <inheritdoc />
    protected override string NotConfiguredHintOf(Store store) =>
        $"adere ao programa Awin da loja e, se tiver feed de produtos, põe o URL do Create-a-Feed em AwinFeeds__Feeds__{store.AwinFeedKey} no .env.";
}
