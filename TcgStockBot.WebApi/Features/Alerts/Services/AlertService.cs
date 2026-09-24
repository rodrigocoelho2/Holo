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

            var newAlerts = currentMatches
                .Where(match => !state.ContainsKey(match.Key))
                .DistinctBy(static match => match.Key)
                .ToList();

            foreach (var alert in newAlerts)
            {
                state[alert.Key] = _timeProvider.GetUtcNow();
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
        }).ToList();

        return _alertRepository.AddHistoryAsync(entries, MaxHistoryEntries);
    }
}
