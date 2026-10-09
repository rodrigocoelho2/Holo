using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

namespace TcgStockBot.WebApi.Features.PriceHistories.Repositories;

/// <summary>
/// price-history.json.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class PriceHistoryRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IPriceHistoryRepository
{
    /// <inheritdoc />
    public async Task<Dictionary<string, PriceHistoryEntry>> ListAsync() =>
        await JsonFile.ReadAsync<Dictionary<string, PriceHistoryEntry>>(DataContext.PriceHistoryPath, CancellationToken.None) ?? [];

    /// <inheritdoc />
    public Task SaveAsync(Dictionary<string, PriceHistoryEntry> entries) =>
        JsonFile.WriteAsync(DataContext.PriceHistoryPath, entries, CancellationToken.None);
}
