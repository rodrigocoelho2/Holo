namespace TcgStockBot.WebApi.Extensions.Data;

/// <summary>
/// Base class of every repository: gives access to the data context and nothing else.
/// Repositories only read and write data — no validation, no business rules.
/// </summary>
/// <param name="dataContext">The data context.</param>
internal abstract class RepositoryBase(BotDataContext dataContext)
{
    /// <summary>Gets the data context.</summary>
    protected BotDataContext DataContext { get; } = dataContext;
}
