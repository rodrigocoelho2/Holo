using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Models.DTOs;
using TcgStockBot.WebApi.Features.Alerts.Models.Entities;
using TcgStockBot.WebApi.Features.Alerts.Repositories;

namespace TcgStockBot.WebApi.Features.Alerts.Services;

/// <summary>
/// Decides which matches are new alerts and keeps the history.
/// </summary>
/// <param name="alertRepository">Alert data access.</param>
/// <param name="timeProvider">Clock.</param>
internal sealed class AlertService(IAlertRepository alertRepository, TimeProvider timeProvider) : IAlertService
{
    private const int MaxHistoryEntries = 500;

    /// <summary>A drop alerts only when it is at least this share of the last alerted price…</summary>
    private const decimal MinimumDropPercent = 0.01m;

    /// <summary>…and at least this amount, so 5,49 € → 5,29 € or 129,99 € → 129,90 € stay quiet.</summary>
    private const decimal MinimumDropAmount = 0.50m;

    private readonly IAlertRepository _alertRepository = alertRepository;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<AlertDTO>>> ListAsync()
    {
        var history = await _alertRepository.ListHistoryAsync();
        return ServiceApiResponse<List<AlertDTO>>.Success(history.Select(static entry => new AlertDTO(entry)).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> ResetAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            await _alertRepository.SaveStateAsync([]);
        }
        finally
        {
            _stateLock.Release();
        }

        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> ClearAsync()
    {
        await _alertRepository.ClearHistoryAsync();
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockAlert>> SelectNewAlertsAsync(IReadOnlyList<StockAlert> currentMatches, IReadOnlySet<string> completedSearches)
    {
        await _stateLock.WaitAsync();
        try
        {
            var state = await _alertRepository.GetStateAsync();
            var currentKeys = currentMatches.Select(static match => match.Key).ToHashSet();

            foreach (var key in state.Keys.ToList())
            {
                var searchKey = key[..key.IndexOf('|', key.IndexOf('|') + 1)];
                if (completedSearches.Contains(searchKey) && !currentKeys.Contains(key))
                {
                    state.Remove(key);
                }
            }

            var newAlerts = new List<StockAlert>();
            foreach (var match in currentMatches.DistinctBy(static match => match.Key))
            {
                if (!state.TryGetValue(match.Key, out var memory))
                {
                    newAlerts.Add(match);
                    state[match.Key] = new AlertMemory { AlertedAt = _timeProvider.GetUtcNow(), Price = match.Listing.Price };
                }
                else if (IsPriceDrop(memory.Price, match.Listing.Price))
                {
                    newAlerts.Add(match with { PreviousPrice = memory.Price });
                    memory.Price = match.Listing.Price;
                }
                else if (memory.Price is null)
                {
                    memory.Price = match.Listing.Price;   // entry from before prices were remembered: start tracking now
                }
            }

            await _alertRepository.SaveStateAsync(state);
            return newAlerts;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc />
    public Task RecordAsync(IReadOnlyList<StockAlert> alerts)
    {
        var now = _timeProvider.GetUtcNow();
        var entries = alerts.Select(alert => new AlertHistory
        {
            At = now,
            WishlistItemId = alert.StockTaskId,
            WishlistItemName = alert.StockTaskName,
            StoreName = alert.Listing.StoreName,
            Title = alert.Listing.Title,
            Url = alert.Listing.Url.AbsoluteUri,
            Price = alert.Listing.Price,
            LanguageToConfirm = alert.LanguageToConfirm,
            ImageUrl = alert.Listing.ImageUrl,
            PreviousPrice = alert.PreviousPrice,
            Msrp = alert.Msrp,
            PreOrder = alert.Listing.PreOrder,
        }).ToList();

        return _alertRepository.AddHistoryAsync(entries, MaxHistoryEntries);
    }

    /// <summary>
    /// Whether the current price is low enough below the last alerted one to alert again. Only the last alerted price
    /// counts: a price that goes up and back down to where it was does not alert twice.
    /// </summary>
    /// <param name="lastAlertedPrice">The price of the last alert.</param>
    /// <param name="currentPrice">The price now.</param>
    /// <returns>True when it is a drop worth an alert.</returns>
    internal static bool IsPriceDrop(decimal? lastAlertedPrice, decimal? currentPrice) =>
        lastAlertedPrice is > 0 && currentPrice is > 0
        && lastAlertedPrice.Value - currentPrice.Value >= MinimumDropAmount
        && lastAlertedPrice.Value - currentPrice.Value >= lastAlertedPrice.Value * MinimumDropPercent;
}
