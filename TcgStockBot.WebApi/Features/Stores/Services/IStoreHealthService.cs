using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Whether stores work: dashboard tests (kept on each store) and failed-cycle counting with automatic turn-off.
/// </summary>
internal interface IStoreHealthService
{
    /// <summary>
    /// Tests one saved store with a search and keeps the result on it.
    /// </summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The test result, or not found.</returns>
    Task<ServiceApiResponse<StoreTestResultDTO>> TestAsync(StoreTestRequest item);

    /// <summary>
    /// Records one check cycle: resets the failure count of stores that answered, counts one more failed cycle for
    /// stores where every search failed, and turns off the enabled ones that reached the limit.
    /// </summary>
    /// <param name="outcomes">Per store searched: true = at least one search worked, false = every search failed.</param>
    /// <param name="disableAfterFailures">Failed cycles in a row before turning a store off (0 = never).</param>
    /// <returns>The stores turned off now, with their failed-cycle count.</returns>
    Task<IReadOnlyList<(string Name, int Failures)>> RecordCycleAsync(IReadOnlyDictionary<string, bool> outcomes, int disableAfterFailures);
}
