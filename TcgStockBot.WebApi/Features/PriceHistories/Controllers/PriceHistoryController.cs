using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.PriceHistories.Models.DTOs;
using TcgStockBot.WebApi.Features.PriceHistories.Services;

namespace TcgStockBot.WebApi.Features.PriceHistories.Controllers;

/// <summary>
/// Price history of the products the monitor sees.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="priceHistoryService">Price history business logic.</param>
[Authorize]
[Route("pricehistory")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PriceHistoryController(
    ILogger<PriceHistoryController> logger,
    IPriceHistoryService priceHistoryService) : InternalControllerBase
{
    private readonly ILogger<PriceHistoryController> _logger = logger;
    private readonly IPriceHistoryService _priceHistoryService = priceHistoryService;

    /// <summary>
    /// Lists every tracked product with its current, lowest and highest price.
    /// </summary>
    /// <returns>The summaries, most recently seen first.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<PriceHistorySummaryDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PriceHistoryList", OperationId = "PriceHistoryList")]
    public async Task<ActionResult<List<PriceHistorySummaryDTO>>> PriceHistoryListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PriceHistoryList");

        var apiResponse = await _priceHistoryService.ListAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// When each store restocks, learnt from the history (weekday and hour, local time).
    /// </summary>
    /// <returns>One pattern per store, most restocks first.</returns>
    [HttpGet("restocks")]
    [ProducesResponseType<List<StoreRestockDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PriceHistoryRestocks", OperationId = "PriceHistoryRestocks")]
    public async Task<ActionResult<List<StoreRestockDTO>>> PriceHistoryRestocksAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PriceHistoryRestocks");

        var apiResponse = await _priceHistoryService.RestocksAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Gets the price history of one product.
    /// </summary>
    /// <param name="identifier">The history key ("store|url").</param>
    /// <returns>The history.</returns>
    [HttpGet]
    [ProducesResponseType<PriceHistoryDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "PriceHistoryGet", OperationId = "PriceHistoryGet")]
    public async Task<ActionResult<PriceHistoryDTO>> PriceHistoryGetAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PriceHistoryGet");

        var apiResponse = await _priceHistoryService.GetAsync(identifier);
        return apiResponse.ToActionResult();
    }
}
