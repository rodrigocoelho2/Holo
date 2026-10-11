using TcgStockBot.WebApi.Features.Users.Models.Entities;

namespace TcgStockBot.WebApi.Features.Users.Repositories;

/// <summary>
/// Data access for users.json.
/// </summary>
internal interface IUserRepository
{
    /// <summary>
    /// Loads every account.
    /// </summary>
    /// <returns>The accounts; empty when the file does not exist yet.</returns>
    Task<List<User>> ListAsync();

    /// <summary>
    /// Replaces every account.
    /// </summary>
    /// <param name="users">The accounts.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveAsync(List<User> users);
}
