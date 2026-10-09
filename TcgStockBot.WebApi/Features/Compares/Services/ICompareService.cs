using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Compares.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Compares.Services;

/// <summary>
/// The same product across stores, from the price history.
/// </summary>
internal interface ICompareService
{
    /// <summary>
    /// Lists every compared product, without the price points.
    /// </summary>
    /// <returns>The groups.</returns>
    Task<ServiceApiResponse<List<CompareGroupDTO>>> ListAsync();

    /// <summary>
    /// Gets one compared product with every store's price points.
    /// </summary>
    /// <param name="identifier">The group key.</param>
    /// <returns>The group, or not found.</returns>
    Task<ServiceApiResponse<CompareGroupDTO>> GetAsync(string identifier);

    /// <summary>
    /// Corrects the grouping of one listing (kept across cycles).
    /// </summary>
    /// <param name="item">The listing and its target group.</param>
    /// <returns>Success.</returns>
    Task<ServiceApiResponse<string>> AssignAsync(CompareAssignRequest item);
}
