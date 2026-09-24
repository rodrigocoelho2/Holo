using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Monitors.Services;

/// <summary>
/// Business logic for the monitor (state, check now, pause, resume).
/// </summary>
internal interface IMonitorService
{
    /// <summary>
    /// Gets what the bot is doing right now.
    /// </summary>
    /// <returns>The monitor state.</returns>
    Task<ServiceApiResponse<MonitorDTO>> GetAsync();

    /// <summary>
    /// Asks for a check cycle now (also while paused).
    /// </summary>
    /// <returns>Success.</returns>
    Task<ServiceApiResponse<string>> RunAsync();

    /// <summary>
    /// Pauses scheduled cycles.
    /// </summary>
    /// <returns>Success.</returns>
    Task<ServiceApiResponse<string>> PauseAsync();

    /// <summary>
    /// Resumes scheduled cycles and checks immediately.
    /// </summary>
    /// <returns>Success.</returns>
    Task<ServiceApiResponse<string>> ResumeAsync();
}
