using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.PriceHistories;

/// <summary>
/// Price history operations for the dashboard.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class PriceHistoryModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>Lists every tracked product.</summary>
    /// <returns>The summaries.</returns>
    public Task<AjaxDataResult<List<PriceHistorySummaryDTO>>> ListAsync() => CallAsync(() => ApiClient.PriceHistoryListAsync());

    /// <summary>Gets the history of one product.</summary>
    /// <param name="identifier">The history key.</param>
    /// <returns>The history.</returns>
    public Task<AjaxDataResult<PriceHistoryDTO>> GetAsync(string identifier) => CallAsync(() => ApiClient.PriceHistoryGetAsync(identifier ?? string.Empty));
}
