using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.StockTasks.Models.Entities;

namespace TcgStockBot.WebApi.Features.StockTasks.Repositories;

/// <summary>
/// Stock tasks stored in bot-config.json ("wishlist").
/// </summary>
/// <param name="dataContext">The data context.</param>
internal sealed class StockTaskRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IStockTaskRepository
{
    /// <inheritdoc />
    public Task<List<StockTask>> ListAsync() => Task.FromResult(DataContext.Snapshot().Wishlist);

    /// <inheritdoc />
    public Task<StockTask?> GetAsync(string identifier) =>
        Task.FromResult(DataContext.Snapshot().Wishlist.FirstOrDefault(task => Matches(task, identifier)));

    /// <inheritdoc />
    public async Task<StockTask> SaveAsync(StockTask item)
    {
        await DataContext.WriteAsync(configuration =>
        {
            var index = configuration.Wishlist.FindIndex(task => Matches(task, item.Id));
            if (index >= 0)
            {
                configuration.Wishlist[index] = item;
            }
            else
            {
                configuration.Wishlist.Add(item);
            }
        }, CancellationToken.None);

        return item;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string identifier) =>
        DataContext.WriteAsync(configuration => configuration.Wishlist.RemoveAll(task => Matches(task, identifier)), CancellationToken.None);

    /// <inheritdoc />
    public Task SetEnabledAsync(string? identifier, bool enabled) =>
        DataContext.WriteAsync(configuration =>
        {
            foreach (var task in configuration.Wishlist.Where(task => string.IsNullOrEmpty(identifier) || Matches(task, identifier)))
            {
                task.Enabled = enabled;
            }
        }, CancellationToken.None);

    /// <inheritdoc />
    public Task RenameStoreAsync(string oldName, string newName) =>
        DataContext.WriteAsync(configuration =>
        {
            foreach (var task in configuration.Wishlist)
            {
                task.StoreNames = task.StoreNames
                    .Select(name => name.Equals(oldName, StringComparison.OrdinalIgnoreCase) ? newName : name)
                    .ToList();
            }
        }, CancellationToken.None);

    /// <inheritdoc />
    public Task RemoveStoreAsync(string storeName) =>
        DataContext.WriteAsync(configuration =>
        {
            foreach (var task in configuration.Wishlist)
            {
                task.StoreNames.RemoveAll(name => name.Equals(storeName, StringComparison.OrdinalIgnoreCase));
            }
        }, CancellationToken.None);

    private static bool Matches(StockTask task, string identifier) =>
        task.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase);
}
