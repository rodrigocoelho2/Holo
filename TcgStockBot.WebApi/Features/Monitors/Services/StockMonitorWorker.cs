using TcgStockBot.WebApi.Features.Settings.Repositories;

namespace TcgStockBot.WebApi.Features.Monitors.Services;

/// <summary>
/// Runs a check cycle every poll interval (+ random jitter) until the app stops.
/// While paused it only runs when the dashboard asks for a check.
/// </summary>
/// <param name="stockCheckService">Runs one cycle.</param>
/// <param name="settingRepository">Interval and pause flag.</param>
/// <param name="scheduler">Wake-up signal from the dashboard.</param>
/// <param name="activity">Dashboard activity record.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed class StockMonitorWorker(
    IStockCheckService stockCheckService,
    ISettingRepository settingRepository,
    CycleScheduler scheduler,
    BotActivity activity,
    TimeProvider timeProvider,
    ILogger<StockMonitorWorker> logger) : BackgroundService
{
    /// <summary>
    /// The monitor loop.
    /// </summary>
    /// <param name="stoppingToken">Signals shutdown.</param>
    /// <returns>A task that completes on shutdown.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runRequested = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!(await settingRepository.GetAsync()).Paused || runRequested)
            {
                try
                {
                    await stockCheckService.RunCycleAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Check cycle crashed; retrying next cycle");
                    activity.Log("error", $"A verificação falhou: {exception.Message}");
                }
            }

            var settings = await settingRepository.GetAsync();
            var wait = settings.Paused
                ? Timeout.InfiniteTimeSpan
                : settings.PollInterval + TimeSpan.FromSeconds(Random.Shared.NextDouble() * settings.PollJitter.TotalSeconds);

            activity.SetNextCheck(settings.Paused ? null : timeProvider.GetUtcNow() + wait);
            runRequested = await scheduler.WaitAsync(wait, timeProvider, stoppingToken);
        }
    }
}
