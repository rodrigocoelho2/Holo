using TcgStockBot.WebApi.Extensions.Data;

namespace TcgStockBot.WebApi.Features.Compares.Repositories;

/// <summary>
/// compare-overrides.json.
/// </summary>
/// <param name="dataContext">The data context (file paths).</param>
internal sealed class CompareOverrideRepository(BotDataContext dataContext) : RepositoryBase(dataContext), ICompareOverrideRepository
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> ListAsync()
    {
        var saved = await JsonFile.ReadAsync<Dictionary<string, string>>(DataContext.CompareOverridesPath, CancellationToken.None);
        return new Dictionary<string, string>(saved ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Dictionary<string, string> overrides)
    {
        await _lock.WaitAsync();
        try
        {
            await JsonFile.WriteAsync(DataContext.CompareOverridesPath, overrides, CancellationToken.None);
        }
        finally
        {
            _lock.Release();
        }
    }
}
