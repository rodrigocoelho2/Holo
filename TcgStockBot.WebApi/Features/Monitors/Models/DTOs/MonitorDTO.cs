namespace TcgStockBot.WebApi.Features.Monitors.Models.DTOs;

/// <summary>
/// What the bot is doing right now.
/// </summary>
public sealed class MonitorDTO
{
    /// <summary>Gets or sets a value indicating whether the bot is paused.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets or sets a value indicating whether a check cycle is running.</summary>
    public bool IsChecking { get; set; }

    /// <summary>Gets or sets when the running cycle started.</summary>
    public DateTimeOffset? CurrentCycleStartedAt { get; set; }

    /// <summary>Gets or sets when the next cycle is due (null while paused or checking).</summary>
    public DateTimeOffset? NextCheckAt { get; set; }

    /// <summary>Gets or sets the last finished cycle.</summary>
    public CycleSummaryDTO? LastCycle { get; set; }

    /// <summary>Gets or sets the recent cycles, newest first.</summary>
    public List<CycleSummaryDTO> RecentCycles { get; set; } = [];

    /// <summary>Gets or sets the latest search of each task on each store.</summary>
    public List<SearchReportDTO> Searches { get; set; } = [];

    /// <summary>Gets or sets the activity feed, newest first.</summary>
    public List<ActivityEventDTO> Events { get; set; } = [];
}
