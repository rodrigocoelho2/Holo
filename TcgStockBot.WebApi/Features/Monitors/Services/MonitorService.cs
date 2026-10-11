using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;
using TcgStockBot.WebApi.Features.Settings.Repositories;

namespace TcgStockBot.WebApi.Features.Monitors.Services;

/// <summary>
/// Default <see cref="IMonitorService"/>.
/// </summary>
/// <param name="settingRepository">Pause flag.</param>
/// <param name="activity">Activity record.</param>
/// <param name="scheduler">Wake-up signal.</param>
internal sealed class MonitorService(
    ISettingRepository settingRepository,
    BotActivity activity,
    CycleScheduler scheduler) : IMonitorService
{
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly BotActivity _activity = activity;
    private readonly CycleScheduler _scheduler = scheduler;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MonitorDTO>> GetAsync()
    {
        var settings = await _settingRepository.GetAsync();
        return ServiceApiResponse<MonitorDTO>.Success(_activity.Snapshot(settings.Paused));
    }

    /// <inheritdoc />
    public Task<ServiceApiResponse<string>> RunAsync()
    {
        _scheduler.RequestRun();
        _activity.Log("info", "Check requested from Card Forge");
        return Task.FromResult(ServiceApiResponse<string>.Success());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> PauseAsync()
    {
        await _settingRepository.SetPausedAsync(true);
        _activity.Log("info", "Bot paused");
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> ResumeAsync()
    {
        await _settingRepository.SetPausedAsync(false);
        _activity.Log("info", "Bot resumed");
        _scheduler.RequestRun();
        return ServiceApiResponse<string>.Success();
    }
}
