using TcgStockBot.WebApi.Features.Push.Models.Entities;

namespace TcgStockBot.WebApi.Features.Push.Repositories;

/// <summary>
/// push-subscriptions.json and push-keys.json.
/// </summary>
internal interface IPushRepository
{
    /// <summary>Reads every subscription.</summary>
    /// <returns>The subscriptions.</returns>
    Task<List<PushSubscription>> ListAsync();

    /// <summary>Replaces every subscription.</summary>
    /// <param name="subscriptions">The subscriptions.</param>
    /// <returns>A task that completes when written.</returns>
    Task SaveAsync(List<PushSubscription> subscriptions);

    /// <summary>Reads the VAPID keys, or null before the first start.</summary>
    /// <returns>The keys.</returns>
    Task<PushKeys?> GetKeysAsync();

    /// <summary>Writes the VAPID keys.</summary>
    /// <param name="keys">The keys.</param>
    /// <returns>A task that completes when written.</returns>
    Task SaveKeysAsync(PushKeys keys);
}
