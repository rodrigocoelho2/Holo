using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Users.Models.Entities;

namespace TcgStockBot.WebApi.Features.Users.Repositories;

/// <summary>
/// users.json.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class UserRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IUserRepository
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<List<User>> ListAsync() =>
        await JsonFile.ReadAsync<List<User>>(DataContext.UsersPath, CancellationToken.None) ?? [];

    /// <inheritdoc />
    public async Task SaveAsync(List<User> users)
    {
        await _lock.WaitAsync();
        try
        {
            await JsonFile.WriteAsync(DataContext.UsersPath, users, CancellationToken.None);
        }
        finally
        {
            _lock.Release();
        }
    }
}
