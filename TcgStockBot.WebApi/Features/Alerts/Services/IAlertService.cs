using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Alerts.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Alerts.Services;

/// <summary>
/// Business logic for alerts.
/// </summary>
internal interface IAlertService
{
    /// <summary>
    /// Lists the alert history, newest first.
    /// </summary>
    /// <returns>The alerts.</returns>
    Task<ServiceApiResponse<List<AlertDTO>>> ListAsync();

    /// <summary>
    /// Forgets every alerted product, so everything currently in stock alerts again.
    /// </summary>
    /// <returns>Success.</returns>
    Task<ServiceApiResponse<string>> ResetAsync();

    /// <summary>
    /// Given what currently matches in the searches that succeeded this cycle, returns only the matches
    /// not alerted yet, and forgets matches that disappeared (sold out / removed) so a restock alerts again.
    /// A search that failed keeps its memory, so a network error never causes a duplicate alert.
    /// </summary>
    /// <param name="currentMatches">Every match found this cycle.</param>
    /// <param name="completedSearches">The <see cref="StockAlert.SearchKey"/> of each search that succeeded.</param>
    /// <returns>The new alerts.</returns>
    Task<IReadOnlyList<StockAlert>> SelectNewAlertsAsync(IReadOnlyList<StockAlert> currentMatches, IReadOnlySet<string> completedSearches);

    /// <summary>
    /// Adds sent alerts to the history.
    /// </summary>
    /// <param name="alerts">The alerts.</param>
    /// <returns>A task that completes when saved.</returns>
    Task RecordAsync(IReadOnlyList<StockAlert> alerts);
}
