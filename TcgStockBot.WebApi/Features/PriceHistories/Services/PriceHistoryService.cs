using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;
using TcgStockBot.WebApi.Features.PriceHistories.Repositories;

namespace TcgStockBot.WebApi.Features.PriceHistories.Services;

/// <summary>
/// Keeps price-history.json small: a point only when the price or the stock changes, at most
/// <see cref="MaxPointsPerProduct"/> per product, and products not seen for <see cref="Retention"/> are dropped.
/// The file is loaded once and kept in memory; every change is written back.
/// </summary>
/// <param name="priceHistoryRepository">History data access.</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="timeProvider">Clock.</param>
internal sealed class PriceHistoryService(
    IPriceHistoryRepository priceHistoryRepository,
    IGlobalizationHelper globalizationHelper,
    TimeProvider timeProvider) : IPriceHistoryService
{
    /// <summary>The most changes kept per product (the oldest go first).</summary>
    internal const int MaxPointsPerProduct = 500;

    /// <summary>How long a product that is no longer seen is kept.</summary>
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(180);

    private const string FieldIdentifier = "Identifier";

    private readonly IPriceHistoryRepository _priceHistoryRepository = priceHistoryRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, PriceHistoryEntry>? _entries;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<PriceHistorySummaryDTO>>> ListAsync()
    {
        var summaries = await ReadAsync(static entries => entries
            .Select(static pair => new PriceHistorySummaryDTO(pair.Key, pair.Value))
            .OrderByDescending(static summary => summary.LastSeen)
            .ToList());

        return ServiceApiResponse<List<PriceHistorySummaryDTO>>.Success(summaries);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PriceHistoryDTO>> GetAsync(string identifier)
    {
        var validationErrors = await ValidateIdentifierAsync(identifier);
        if (validationErrors.Count > 0)
        {
            return ServiceApiResponse<PriceHistoryDTO>.ValidationProblems(
                await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddErrorRange(validationErrors);
        }

        var key = identifier.Trim().ToLowerInvariant();
        var history = await ReadAsync(entries => entries.TryGetValue(key, out var entry) ? new PriceHistoryDTO(key, entry) : null);

        return history is null
            ? ServiceApiResponse<PriceHistoryDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier))
            : ServiceApiResponse<PriceHistoryDTO>.Success(history);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<StoreRestockDTO>>> RestocksAsync()
    {
        var now = _timeProvider.GetUtcNow();
        var patterns = await ReadAsync(entries => RestockPatterns.Find(entries.Values, _timeProvider.LocalTimeZone, now));
        return ServiceApiResponse<List<StoreRestockDTO>>.Success(patterns);
    }

    /// <inheritdoc />
    public async Task RecordAsync(IReadOnlyCollection<PriceObservation> observations)
    {
        await _lock.WaitAsync();
        try
        {
            var entries = await LoadAsync();
            var now = _timeProvider.GetUtcNow();

            foreach (var sameProduct in observations.GroupBy(static observation => observation.Key))
            {
                var observation = sameProduct.First();
                if (!entries.TryGetValue(observation.Key, out var entry))
                {
                    entry = new PriceHistoryEntry { StoreName = observation.StoreName, FirstSeen = now };
                    entries[observation.Key] = entry;
                }

                entry.Title = observation.Title;
                entry.Url = observation.Url;
                entry.ProductId = observation.ProductId ?? entry.ProductId;
                entry.ImageUrl = observation.ImageUrl ?? entry.ImageUrl;
                entry.Currency = observation.Currency;
                entry.LastSeen = now;
                foreach (var taskId in sameProduct.Select(static seen => seen.StockTaskId).OfType<string>())
                {
                    if (!entry.StockTaskIds.Contains(taskId, StringComparer.OrdinalIgnoreCase))
                    {
                        entry.StockTaskIds.Add(taskId);
                    }
                }

                var last = entry.Points.Count > 0 ? entry.Points[^1] : null;
                if (last is null || last.Price != observation.Price || last.InStock != observation.InStock)
                {
                    entry.Points.Add(new PricePoint { At = now, Price = observation.Price, InStock = observation.InStock });
                    if (entry.Points.Count > MaxPointsPerProduct)
                    {
                        entry.Points.RemoveRange(0, entry.Points.Count - MaxPointsPerProduct);
                    }
                }
            }

            foreach (var key in entries.Where(pair => now - pair.Value.LastSeen > Retention).Select(static pair => pair.Key).ToList())
            {
                entries.Remove(key);
            }

            await _priceHistoryRepository.SaveAsync(entries);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, decimal>> LowestPricesAsync() =>
        ReadAsync<IReadOnlyDictionary<string, decimal>>(static entries => entries
            .Where(static pair => pair.Value.Points.Count > 0)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value.Points.Min(static point => point.Price)));

    /// <inheritdoc />
    public Task<Dictionary<string, PriceHistoryEntry>> SnapshotAsync() =>
        ReadAsync(static entries => JsonFile.Clone(entries));

    private async Task<T> ReadAsync<T>(Func<Dictionary<string, PriceHistoryEntry>, T> read)
    {
        await _lock.WaitAsync();
        try
        {
            return read(await LoadAsync());
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Loads the file on first use; callers hold the lock.</summary>
    private async Task<Dictionary<string, PriceHistoryEntry>> LoadAsync() =>
        _entries ??= new Dictionary<string, PriceHistoryEntry>(await _priceHistoryRepository.ListAsync(), StringComparer.OrdinalIgnoreCase);

    private async Task<List<ApiValidationError>> ValidateIdentifierAsync(string identifier)
    {
        var validationErrors = new List<ApiValidationError>();

        if (string.IsNullOrWhiteSpace(identifier))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.RequiredField)));
        }

        return validationErrors;
    }
}
