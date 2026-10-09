using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Alerts.Models.Entities;

namespace TcgStockBot.WebApi.Features.Alerts.Repositories;

/// <summary>
/// alert-history.json and alert-state.json.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class AlertRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IAlertRepository
{
    private readonly SemaphoreSlim _historyLock = new(1, 1);

    /// <inheritdoc />
    public async Task<List<AlertHistory>> ListHistoryAsync() =>
        await JsonFile.ReadAsync<List<AlertHistory>>(DataContext.AlertHistoryPath, CancellationToken.None) ?? [];

    /// <inheritdoc />
    public async Task AddHistoryAsync(IReadOnlyList<AlertHistory> entries, int maxEntries)
    {
        await _historyLock.WaitAsync();
        try
        {
            var history = await ListHistoryAsync();
            history.InsertRange(0, entries);

            if (history.Count > maxEntries)
            {
                history.RemoveRange(maxEntries, history.Count - maxEntries);
            }

            await JsonFile.WriteAsync(DataContext.AlertHistoryPath, history, CancellationToken.None);
        }
        finally
        {
            _historyLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearHistoryAsync()
    {
        await _historyLock.WaitAsync();
        try
        {
            await JsonFile.WriteAsync(DataContext.AlertHistoryPath, new List<AlertHistory>(), CancellationToken.None);
        }
        finally
        {
            _historyLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, AlertMemory>> GetStateAsync() =>
        await JsonFile.ReadAsync<Dictionary<string, AlertMemory>>(DataContext.AlertStatePath, CancellationToken.None) ?? [];

    /// <inheritdoc />
    public Task SaveStateAsync(Dictionary<string, AlertMemory> state) =>
        JsonFile.WriteAsync(DataContext.AlertStatePath, state, CancellationToken.None);
}
