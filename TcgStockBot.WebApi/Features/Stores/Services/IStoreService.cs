using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Business logic for stores.
/// </summary>
internal interface IStoreService
{
    /// <summary>
    /// Lists every store.
    /// </summary>
    /// <returns>The stores.</returns>
    Task<ServiceApiResponse<List<StoreDTO>>> ListAsync();

    /// <summary>
    /// Creates, replaces or renames a store. A rename also updates the tasks that use it.
    /// </summary>
    /// <param name="item">The store data.</param>
    /// <returns>The saved name, or validation problems.</returns>
    Task<ServiceApiResponse<StoreSaveResponse>> SaveAsync(StoreSaveRequest item);

    /// <summary>
    /// Deletes a store and removes it from every task.
    /// </summary>
    /// <param name="name">The store name.</param>
    /// <returns>Success, or validation problems.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(string name);

    /// <summary>
    /// Turns a store on or off.
    /// </summary>
    /// <param name="item">What to change.</param>
    /// <returns>Success, or not found.</returns>
    Task<ServiceApiResponse<string>> EnableAsync(StoreEnableRequest item);

    /// <summary>
    /// Runs one search on a store configuration (saved or not) and explains each listing per task.
    /// </summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The listings, or the reason the store could not be searched.</returns>
    Task<ServiceApiResponse<List<StoreProbeResult>>> ProbeAsync(StoreProbeRequest item);
}
