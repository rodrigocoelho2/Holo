using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Stores.Models.Entities;

namespace TcgStockBot.WebApi.Features.Stores.Repositories;

/// <summary>
/// Stores kept in bot-config.json.
/// </summary>
/// <param name="dataContext">The data context.</param>
internal sealed class StoreRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IStoreRepository
{
    /// <inheritdoc />
    public Task<List<Store>> ListAsync() => Task.FromResult(DataContext.Snapshot().Stores);

    /// <inheritdoc />
    public Task<Store?> GetAsync(string name) =>
        Task.FromResult(DataContext.Snapshot().Stores.FirstOrDefault(store => Matches(store, name)));

    /// <inheritdoc />
    public async Task<Store> SaveAsync(Store item, string? originalName)
    {
        await DataContext.WriteAsync(configuration =>
        {
            var index = originalName is null ? -1 : configuration.Stores.FindIndex(store => Matches(store, originalName));
            if (index >= 0)
            {
                configuration.Stores[index] = item;
            }
            else
            {
                configuration.Stores.Add(item);
            }
        }, CancellationToken.None);

        return item;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string name) =>
        DataContext.WriteAsync(configuration => configuration.Stores.RemoveAll(store => Matches(store, name)), CancellationToken.None);

    /// <inheritdoc />
    public Task SetEnabledAsync(string name, bool enabled) =>
        DataContext.WriteAsync(configuration =>
        {
            foreach (var store in configuration.Stores.Where(store => Matches(store, name)))
            {
                store.Enabled = enabled;
            }
        }, CancellationToken.None);

    private static bool Matches(Store store, string name) =>
        store.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
}
