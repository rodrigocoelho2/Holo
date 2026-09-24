using TcgStockBot.WebApi.Features.Settings.Models.Entities;

namespace TcgStockBot.WebApi.Features.Settings.Models.DTOs;

/// <summary>
/// Input of SettingSave. Pausing is done with MonitorPause / MonitorResume, not here.
/// </summary>
public sealed class SettingSaveRequest
{
    /// <summary>Gets or sets the time between two check cycles, in seconds (minimum 60).</summary>
    public int PollIntervalSeconds { get; set; }

    /// <summary>Gets or sets the random extra wait, in seconds.</summary>
    public int PollJitterSeconds { get; set; }

    /// <summary>Gets or sets the pause between requests to the same store, in seconds.</summary>
    public int DelayBetweenRequestsSeconds { get; set; }

    /// <summary>Gets or sets the User-Agent sent to stores.</summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Maps the request to the entity, keeping the current pause state.
    /// </summary>
    /// <param name="paused">The current pause state.</param>
    /// <returns>The entity to store.</returns>
    public Setting SettingNew(bool paused) => new()
    {
        Paused = paused,
        PollInterval = TimeSpan.FromSeconds(PollIntervalSeconds),
        PollJitter = TimeSpan.FromSeconds(PollJitterSeconds),
        DelayBetweenRequests = TimeSpan.FromSeconds(DelayBetweenRequestsSeconds),
        UserAgent = (UserAgent ?? string.Empty).Trim(),
    };
}
