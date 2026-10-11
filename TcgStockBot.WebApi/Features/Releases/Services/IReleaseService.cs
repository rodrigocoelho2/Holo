using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Releases.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Releases.Services;

/// <summary>
/// The launch calendar.
/// </summary>
internal interface IReleaseService
{
    /// <summary>
    /// Lists every release, soonest first.
    /// </summary>
    /// <returns>The releases.</returns>
    Task<ServiceApiResponse<List<ReleaseDTO>>> ListAsync();

    /// <summary>
    /// Creates or replaces a release.
    /// </summary>
    /// <param name="item">The release.</param>
    /// <returns>The saved release, or validation problems.</returns>
    Task<ServiceApiResponse<ReleaseDTO>> SaveAsync(ReleaseSaveRequest item);

    /// <summary>
    /// Deletes a release.
    /// </summary>
    /// <param name="identifier">The release identifier.</param>
    /// <returns>Success, or not found.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string identifier);
}
