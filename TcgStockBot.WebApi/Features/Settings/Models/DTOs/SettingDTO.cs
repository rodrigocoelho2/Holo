using TcgStockBot.WebApi.Features.Settings.Models.Entities;

namespace TcgStockBot.WebApi.Features.Settings.Models.DTOs;

/// <summary>
/// Read model of the monitoring settings. Durations are in seconds.
/// </summary>
public sealed class SettingDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public SettingDTO()
    {
    }

    /// <summary>
    /// Maps the entity to the read model.
    /// </summary>
    /// <param name="entity">The stored settings.</param>
    public SettingDTO(Setting entity)
    {
        Paused = entity.Paused;
        PollIntervalSeconds = (int)entity.PollInterval.TotalSeconds;
        PollJitterSeconds = (int)entity.PollJitter.TotalSeconds;
        DelayBetweenRequestsSeconds = (int)entity.DelayBetweenRequests.TotalSeconds;
        UserAgent = entity.UserAgent;
    }

    /// <summary>Gets or sets a value indicating whether the bot is paused.</summary>
    public bool Paused { get; set; }

    /// <summary>Gets or sets the time between two check cycles, in seconds.</summary>
    public int PollIntervalSeconds { get; set; }

    /// <summary>Gets or sets the random extra wait, in seconds.</summary>
    public int PollJitterSeconds { get; set; }

    /// <summary>Gets or sets the pause between requests to the same store, in seconds.</summary>
    public int DelayBetweenRequestsSeconds { get; set; }

    /// <summary>Gets or sets the User-Agent sent to stores.</summary>
    public string UserAgent { get; set; } = string.Empty;
}
