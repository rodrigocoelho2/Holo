using Microsoft.AspNetCore.Mvc;
using TcgStockBot.Sdk;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Base of every Website controller. Controllers are thin: create the model, call it, return its result.
/// </summary>
/// <param name="apiClient">The TcgStockBot.WebApi client handed to the models.</param>
public abstract class WebsiteBaseController(TcgStockBotClient apiClient) : Controller
{
    /// <summary>Gets the API client.</summary>
    protected TcgStockBotClient ApiClient { get; } = apiClient;
}
