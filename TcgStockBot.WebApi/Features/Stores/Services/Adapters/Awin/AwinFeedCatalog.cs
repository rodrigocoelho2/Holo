using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.Extensions.Options;
using TcgStockBot.Library.Text;
using TcgStockBot.WebApi.Extensions.Http;

namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

/// <summary>
/// One product of an Awin feed, already normalized.
/// </summary>
/// <param name="Title">The product name.</param>
/// <param name="NormalizedTitle">The title lower-cased and without accents, for searching.</param>
/// <param name="Url">The product page.</param>
/// <param name="Price">The current price.</param>
/// <param name="Currency">ISO 4217 currency.</param>
/// <param name="InStock">Whether the feed says it is in stock.</param>
/// <param name="ProductId">The merchant's product id, Awin's id or the EAN.</param>
/// <param name="Seller">The merchant name in the feed.</param>
/// <param name="PreOrder">Whether stock_status says pre-order.</param>
/// <param name="ImageUrl">The product photo, when the feed has one.</param>
internal sealed record AwinFeedProduct(
    string Title,
    string NormalizedTitle,
    Uri Url,
    decimal? Price,
    string Currency,
    bool InStock,
    string? ProductId,
    string? Seller,
    bool PreOrder = false,
    string? ImageUrl = null);

/// <summary>
/// Downloads and caches Awin product feeds (CSV, optionally gzip). A feed is downloaded at most once per
/// RefreshInterval and shared by every task, instead of one request per search — Awin feeds change a few times
/// a day, and Awin asks publishers not to download them more often than that. Only products whose title contains
/// one of KeepKeywords are kept in memory.
/// </summary>
/// <param name="options">Feed settings.</param>
/// <param name="httpClientFactory">Creates the outbound client.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed class AwinFeedCatalog(
    IOptionsMonitor<AwinFeedOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<AwinFeedCatalog> logger)
{
    /// <summary>The named HttpClient for feed downloads.</summary>
    public const string HttpClientName = "awin-feeds";

    private static readonly string[] PriceColumns = ["search_price", "store_price", "display_price"];
    private static readonly string[] UrlColumns = ["merchant_deep_link", "aw_deep_link"];
    private static readonly string[] IdColumns = ["merchant_product_id", "aw_product_id", "ean", "product_gtin"];
    private static readonly string[] ImageColumns = ["merchant_image_url", "large_image", "aw_image_url", "aw_thumb_url"];

    private readonly ConcurrentDictionary<string, FeedState> _feeds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the cached products of a feed, downloading it when missing or older than RefreshInterval.
    /// </summary>
    /// <param name="provider">The provider name for messages and logs (e.g. "El Corte Inglés").</param>
    /// <param name="feedKey">The key in AwinFeeds:Feeds.</param>
    /// <param name="feedUrl">The feed download URL (a secret: never logged).</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The kept products.</returns>
    public async Task<IReadOnlyList<AwinFeedProduct>> GetProductsAsync(string provider, string feedKey, string feedUrl, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var state = _feeds.GetOrAdd(feedKey, static _ => new FeedState());

        if (TryCached(state, feedUrl, settings) is { } cached)
        {
            return cached;
        }

        await state.Lock.WaitAsync(cancellationToken);
        try
        {
            if (TryCached(state, feedUrl, settings) is { } fresh)
            {
                return fresh;
            }

            try
            {
                var products = await DownloadAsync(provider, feedKey, feedUrl, settings, cancellationToken);
                state.Url = feedUrl;
                state.Products = products;
                state.LoadedAt = timeProvider.GetUtcNow();
                state.Failure = null;
                return products;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Remember the failure so the other tasks of this cycle do not hammer the feed server.
                state.Failure = ExceptionDispatchInfo.Capture(exception);
                state.FailedAt = timeProvider.GetUtcNow();
                throw;
            }
        }
        finally
        {
            state.Lock.Release();
        }
    }

    /// <summary>
    /// Parses a feed (CSV, or gzip-compressed CSV).
    /// </summary>
    /// <param name="provider">The provider name for messages.</param>
    /// <param name="content">The feed bytes.</param>
    /// <param name="settings">The settings (KeepKeywords, MaxProducts).</param>
    /// <param name="cancellationToken">Cancels the parse.</param>
    /// <returns>The kept products and the number of rows read.</returns>
    internal static (List<AwinFeedProduct> Products, int Rows) Parse(string provider, Stream content, AwinFeedOptions settings, CancellationToken cancellationToken)
    {
        using var source = IsGzip(content, out var prefixed) ? new GZipStream(prefixed, CompressionMode.Decompress) : prefixed;
        using var text = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var csv = new CsvRecordReader(text);

        var header = csv.ReadRecord(cancellationToken)
            ?? throw new StoreIntegrationException(provider, StoreIntegrationProblem.ApiError, "the Awin feed came back empty.");

        if (header.Count == 1 && (header[0].Contains('|') || header[0].Contains('\t')))
        {
            // Create-a-Feed also offers pipe and tab delimiters.
            csv.Delimiter = header[0].Contains('|') ? '|' : '\t';
            header = [.. header[0].Split(csv.Delimiter)];
        }

        var columns = header
            .Select(static (name, index) => (Name: name.Trim().ToLowerInvariant(), Index: index))
            .GroupBy(static column => column.Name)
            .ToDictionary(static group => group.Key, static group => group.First().Index);

        var missing = new List<string>();
        if (!columns.ContainsKey("product_name")) missing.Add("product_name");
        if (!PriceColumns.Any(columns.ContainsKey)) missing.Add("search_price");
        if (!UrlColumns.Any(columns.ContainsKey)) missing.Add("merchant_deep_link");
        if (missing.Count > 0)
        {
            throw new StoreIntegrationException(provider, StoreIntegrationProblem.ApiError,
                $"the Awin feed is missing the columns {string.Join(", ", missing)}. Create it in Create-a-Feed with product_name, search_price, currency, in_stock, stock_status, merchant_deep_link, aw_deep_link, merchant_product_id and merchant_name.");
        }

        var keep = settings.KeepKeywords.Where(static word => !string.IsNullOrWhiteSpace(word)).Select(TextNormalizer.Normalize).ToList();
        var products = new List<AwinFeedProduct>();
        var rows = 0;

        while (csv.ReadRecord(cancellationToken) is { } record)
        {
            rows++;
            var title = Field(record, columns, "product_name");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var normalizedTitle = TextNormalizer.Normalize(title);
            if (keep.Count > 0 && !keep.Any(normalizedTitle.Contains))
            {
                continue;
            }

            var url = UrlColumns.Select(name => Field(record, columns, name)).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
            if (!Uri.TryCreate(url, UriKind.Absolute, out var productUrl))
            {
                continue;
            }

            if (products.Count >= settings.MaxProducts)
            {
                break;
            }

            products.Add(new AwinFeedProduct(
                title.Trim(),
                normalizedTitle,
                productUrl,
                PriceColumns.Select(name => ParsePrice(Field(record, columns, name))).FirstOrDefault(static price => price is > 0),
                Field(record, columns, "currency") is { Length: 3 } currency ? currency.ToUpperInvariant() : "EUR",
                ParseInStock(Field(record, columns, "in_stock"), Field(record, columns, "stock_status"), Field(record, columns, "stock_quantity")),
                IdColumns.Select(name => Field(record, columns, name)).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
                Field(record, columns, "merchant_name") is { Length: > 0 } seller ? seller : null,
                IsPreOrderStatus(Field(record, columns, "stock_status")),
                ImageColumns.Select(name => StoreImageUrl.Normalize(Field(record, columns, name))).FirstOrDefault(static image => image is not null)));
        }

        return (products, rows);
    }

    /// <summary>
    /// Awin's stock columns: in_stock 1/0 (blank = no stock information, other text = in stock); otherwise
    /// stock_quantity or a stock_status text.
    /// </summary>
    internal static bool ParseInStock(string? inStock, string? stockStatus, string? stockQuantity)
    {
        if (!string.IsNullOrWhiteSpace(inStock))
        {
            return TextNormalizer.Normalize(inStock.Trim()) is not ("0" or "false" or "no" or "nao" or "n");
        }

        if (int.TryParse(stockQuantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            return quantity > 0;
        }

        if (string.IsNullOrWhiteSpace(stockStatus))
        {
            return false;
        }

        var status = TextNormalizer.Normalize(stockStatus).Replace("_", " ").Replace("-", " ");
        if (status.Contains("out of stock") || status.Contains("outofstock") || status.Contains("esgotado") || status.Contains("agotado")
            || status.Contains("sem stock") || status.Contains("unavailable") || status.Contains("indisponivel"))
        {
            return false;
        }

        return status.Contains("in stock") || status.Contains("instock") || status.Contains("available") || status.Contains("preorder")
            || status.Contains("pre order") || status.Contains("disponivel") || status.Contains("em stock") || status.Contains("en stock");
    }

    /// <summary>Awin stock_status "preorder" / "pre-order" / "pre_order".</summary>
    internal static bool IsPreOrderStatus(string? stockStatus) =>
        !string.IsNullOrWhiteSpace(stockStatus)
        && TextNormalizer.Normalize(stockStatus).Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Contains("preorder");

    private IReadOnlyList<AwinFeedProduct>? TryCached(FeedState state, string feedUrl, AwinFeedOptions settings)
    {
        var now = timeProvider.GetUtcNow();
        var sameFeed = state.Url == feedUrl;

        if (sameFeed && state.Products is { } products && now - state.LoadedAt < settings.EffectiveRefreshInterval())
        {
            return products;
        }

        if (state.Failure is { } failure && now - state.FailedAt < settings.RetryAfterFailure)
        {
            if (sameFeed && state.Products is { } stale)
            {
                return stale;   // a few hours old is better than nothing; the next retry is due soon
            }

            failure.Throw();
        }

        return null;
    }

    private async Task<IReadOnlyList<AwinFeedProduct>> DownloadAsync(string provider, string feedKey, string feedUrl, AwinFeedOptions settings, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(feedUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new StoreIntegrationException(provider, StoreIntegrationProblem.NotConfigured,
                $"AwinFeeds__Feeds__{feedKey} is not a valid https URL (copy the download URL from Create-a-Feed).");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        // The feed API key is part of the path: the logs only ever show the host and the feed key.
        request.ForProvider(provider, uri.Host, logTarget: $"{uri.Host}/… (Awin feed {feedKey})");

        using var response = await httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                throw new StoreIntegrationException(provider, StoreIntegrationProblem.Unauthorized,
                    $"Awin refused the feed download (HTTP {(int)response.StatusCode}). Check that the programme approved you and that the Create-a-Feed URL is complete.");
            case HttpStatusCode.NotFound:
                throw new StoreIntegrationException(provider, StoreIntegrationProblem.ApiError,
                    "Awin could not find this feed (HTTP 404). Create a new URL in Create-a-Feed.");
            case HttpStatusCode.TooManyRequests:
            case >= HttpStatusCode.InternalServerError:
                response.EnsureSuccessStatusCode();   // → "asked to slow down" / "error on the store's side"
                break;
            case var status when (int)status >= 400:
                throw new StoreIntegrationException(provider, StoreIntegrationProblem.ApiError, $"Awin answered HTTP {(int)status} to the feed download.");
        }

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        // The body is read synchronously on a worker thread; cancelling (bot stopping) aborts it by disposing the response.
        await using var abort = cancellationToken.Register(static state => ((HttpResponseMessage)state!).Dispose(), response);
        var (products, rows) = await Task.Run(() => Parse(provider, content, settings, cancellationToken), cancellationToken);

        if (products.Count >= settings.MaxProducts && logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning("Awin feed {FeedKey}: stopped at MaxProducts ({MaxProducts}); narrow AwinFeeds:KeepKeywords", feedKey, settings.MaxProducts);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Awin feed {FeedKey} loaded: {Kept} of {Rows} products kept", feedKey, products.Count, rows);

        return products;
    }

    private static string? Field(List<string> record, Dictionary<string, int> columns, string name) =>
        columns.TryGetValue(name, out var index) && index < record.Count ? record[index] : null;

    private static decimal? ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Awin prices are plain decimals ("39.99"); fall back to the generic parser for "39,99 €"-style values.
        return decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && !value.Contains(',')
            ? price
            : PriceParser.Parse(value);
    }

    /// <summary>Detects the gzip magic number without consuming it.</summary>
    private static bool IsGzip(Stream content, out Stream prefixed)
    {
        var head = new byte[2];
        var read = 0;
        while (read < head.Length)
        {
            var count = content.Read(head, read, head.Length - read);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        prefixed = new PrefixedStream(head.AsMemory(0, read), content);
        return read == 2 && head[0] == 0x1f && head[1] == 0x8b;
    }

    private sealed class FeedState
    {
        public SemaphoreSlim Lock { get; } = new(1, 1);

        public string? Url { get; set; }

        public IReadOnlyList<AwinFeedProduct>? Products { get; set; }

        public DateTimeOffset LoadedAt { get; set; }

        public ExceptionDispatchInfo? Failure { get; set; }

        public DateTimeOffset FailedAt { get; set; }
    }

    /// <summary>A read-only stream that first returns some already-read bytes, then the rest of the inner stream.</summary>
    private sealed class PrefixedStream(ReadOnlyMemory<byte> prefix, Stream inner) : Stream
    {
        private ReadOnlyMemory<byte> _prefix = prefix;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_prefix.IsEmpty)
            {
                return inner.Read(buffer);
            }

            var count = Math.Min(buffer.Length, _prefix.Length);
            _prefix.Span[..count].CopyTo(buffer);
            _prefix = _prefix[count..];
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
