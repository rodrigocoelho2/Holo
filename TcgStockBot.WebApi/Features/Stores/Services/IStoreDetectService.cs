using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Finds out how a store can be searched (Shopify, WooCommerce, PrestaShop search page) from its address.
/// </summary>
internal interface IStoreDetectService
{
    /// <summary>
    /// Detects a store's platform with a few plain requests to its public endpoints.
    /// </summary>
    /// <param name="item">The store address.</param>
    /// <returns>The suggested configuration.</returns>
    Task<ServiceApiResponse<StoreDetectResult>> DetectAsync(StoreDetectRequest item);
}
