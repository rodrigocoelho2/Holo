namespace TcgStockBot.WebApi.Features.Monitors.Models.DTOs;

/// <summary>
/// One line of the activity feed.
/// </summary>
public sealed class ActivityEventDTO
{
    /// <summary>Gets or sets when it happened.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Gets or sets the level: info, warn, error or alert.</summary>
    public string Level { get; set; } = "info";

    /// <summary>Gets or sets the message.</summary>
    public string Message { get; set; } = string.Empty;
}
