namespace TcgStockBot.WebApi.Features.Settings.Models.Entities;

/// <summary>
/// The monitoring settings (the scalar fields of bot-config.json).
/// </summary>
public sealed class Setting
{
    /// <summary>Gets or sets a value indicating whether the scheduler skips cycles.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets or sets the time between two check cycles.</summary>
    public TimeSpan PollInterval { get; set; }

    /// <summary>Gets or sets the random extra wait added to each cycle.</summary>
    public TimeSpan PollJitter { get; set; }

    /// <summary>Gets or sets the pause between two requests to the same store.</summary>
    public TimeSpan DelayBetweenRequests { get; set; }

    /// <summary>Gets or sets the User-Agent sent to stores.</summary>
    public string UserAgent { get; set; } = string.Empty;
}
