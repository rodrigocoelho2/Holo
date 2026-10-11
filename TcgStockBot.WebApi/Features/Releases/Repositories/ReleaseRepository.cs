using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Releases.Models.Entities;

namespace TcgStockBot.WebApi.Features.Releases.Repositories;

/// <summary>
/// releases.json.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class ReleaseRepository(BotDataContext dataContext) : RepositoryBase(dataContext), IReleaseRepository
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<List<Release>> ListAsync() =>
        await JsonFile.ReadAsync<List<Release>>(DataContext.ReleasesPath, CancellationToken.None) ?? [];

    /// <inheritdoc />
    public async Task SaveAsync(List<Release> releases)
    {
        await _lock.WaitAsync();
        try
        {
            await JsonFile.WriteAsync(DataContext.ReleasesPath, releases, CancellationToken.None);
        }
        finally
        {
            _lock.Release();
        }
    }
}
