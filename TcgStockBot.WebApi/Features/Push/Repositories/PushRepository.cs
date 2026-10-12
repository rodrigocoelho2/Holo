using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Push.Models.Entities;

namespace TcgStockBot.WebApi.Features.Push.Repositories;

/// <summary>
/// push-subscriptions.json and push-keys.json.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class PushRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IPushRepository
{
    /// <inheritdoc />
    public async Task<List<PushSubscription>> ListAsync() =>
        await JsonFile.ReadAsync<List<PushSubscription>>(DataContext.PushSubscriptionsPath, CancellationToken.None) ?? [];

    /// <inheritdoc />
    public Task SaveAsync(List<PushSubscription> subscriptions) =>
        JsonFile.WriteAsync(DataContext.PushSubscriptionsPath, subscriptions, CancellationToken.None);

    /// <inheritdoc />
    public Task<PushKeys?> GetKeysAsync() => JsonFile.ReadAsync<PushKeys>(DataContext.PushKeysPath, CancellationToken.None);

    /// <inheritdoc />
    public Task SaveKeysAsync(PushKeys keys) => JsonFile.WriteAsync(DataContext.PushKeysPath, keys, CancellationToken.None);
}
