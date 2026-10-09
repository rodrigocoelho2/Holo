using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;
using TcgStockBot.WebApi.Features.PriceHistories.Models.Entities;

namespace TcgStockBot.WebApi.Features.PriceHistories.Services;

/// <summary>
/// Price history of the relevant products the monitor sees.
/// </summary>
internal interface IPriceHistoryService
{
    /// <summary>
    /// Lists every tracked product, most recently seen first.
    /// </summary>
    /// <returns>The summaries.</returns>
    Task<ServiceApiResponse<List<PriceHistorySummaryDTO>>> ListAsync();

    /// <summary>
    /// Gets the history of one product.
    /// </summary>
    /// <param name="identifier">The history key.</param>
    /// <returns>The history, or not found.</returns>
    Task<ServiceApiResponse<PriceHistoryDTO>> GetAsync(string identifier);

    /// <summary>
    /// Adds what one check cycle saw: a point only when the price or the stock changed. Several observations of the
    /// same product (one per task) are merged.
    /// </summary>
    /// <param name="observations">The products seen.</param>
    /// <returns>A task that completes when saved.</returns>
    Task RecordAsync(IReadOnlyCollection<PriceObservation> observations);

    /// <summary>
    /// The lowest price seen per history key, for the monitor's listing reports.
    /// </summary>
    /// <returns>Key → lowest price.</returns>
    Task<IReadOnlyDictionary<string, decimal>> LowestPricesAsync();

    /// <summary>
    /// An independent copy of every history (key → entry), for the Compare view.
    /// </summary>
    /// <returns>The histories.</returns>
    Task<Dictionary<string, PriceHistoryEntry>> SnapshotAsync();
}
