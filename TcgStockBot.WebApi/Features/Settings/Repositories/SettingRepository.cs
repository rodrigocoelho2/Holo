using TcgStockBot.WebApi.Extensions.Data;
using TcgStockBot.WebApi.Features.Settings.Models.Entities;

namespace TcgStockBot.WebApi.Features.Settings.Repositories;

/// <summary>
/// The scalar settings of bot-config.json.
/// </summary>
/// <param name="dataContext">The data context.</param>
internal sealed class SettingRepository(BotDataContext dataContext) : RepositoryBase(dataContext), ISettingRepository
{
    /// <inheritdoc />
    public Task<Setting> GetAsync()
    {
        var configuration = DataContext.Snapshot();
        return Task.FromResult(new Setting
        {
            Paused = configuration.Paused,
            PollInterval = configuration.PollInterval,
            PollJitter = configuration.PollJitter,
            DelayBetweenRequests = configuration.DelayBetweenRequests,
            UserAgent = configuration.UserAgent,
        });
    }

    /// <inheritdoc />
    public async Task<Setting> SaveAsync(Setting item)
    {
        await DataContext.WriteAsync(configuration =>
        {
            configuration.Paused = item.Paused;
            configuration.PollInterval = item.PollInterval;
            configuration.PollJitter = item.PollJitter;
            configuration.DelayBetweenRequests = item.DelayBetweenRequests;
            configuration.UserAgent = item.UserAgent;
        }, CancellationToken.None);

        return item;
    }

    /// <inheritdoc />
    public Task SetPausedAsync(bool paused) =>
        DataContext.WriteAsync(configuration => configuration.Paused = paused, CancellationToken.None);
}
