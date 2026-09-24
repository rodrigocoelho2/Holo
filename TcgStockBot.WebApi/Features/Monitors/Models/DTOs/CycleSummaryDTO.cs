namespace TcgStockBot.WebApi.Features.Monitors.Models.DTOs;

/// <summary>
/// The outcome of one check cycle.
/// </summary>
public sealed class CycleSummaryDTO
{
    /// <summary>Gets or sets when the cycle started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets when the cycle finished.</summary>
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>Gets or sets how many searches ran.</summary>
    public int Searches { get; set; }

    /// <summary>Gets or sets how many searches failed.</summary>
    public int Failed { get; set; }

    /// <summary>Gets or sets how many listings matched.</summary>
    public int Matches { get; set; }

    /// <summary>Gets or sets how many new alerts were sent.</summary>
    public int NewAlerts { get; set; }
}
