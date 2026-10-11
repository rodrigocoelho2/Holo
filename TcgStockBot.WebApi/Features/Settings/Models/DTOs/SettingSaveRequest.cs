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

    /// <summary>Gets or sets how many failed cycles in a row turn a store off (0 = never).</summary>
    public int AutoDisableAfterFailures { get; set; } = 6;

    /// <summary>Gets or sets a value indicating whether the daily summary e-mail is on.</summary>
    public bool DailySummaryEnabled { get; set; }

    /// <summary>Gets or sets the local hour (0–23) of the daily summary.</summary>
    public int DailySummaryHour { get; set; } = 9;

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
        AutoDisableAfterFailures = AutoDisableAfterFailures,
        DailySummaryEnabled = DailySummaryEnabled,
        DailySummaryHour = DailySummaryHour,
    };
}
