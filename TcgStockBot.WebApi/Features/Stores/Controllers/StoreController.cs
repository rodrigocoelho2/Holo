using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Services;

namespace TcgStockBot.WebApi.Features.Stores.Controllers;

/// <summary>
/// Stores the bot searches.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="storeService">Store business logic.</param>
[Authorize]
[Route("store")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class StoreController(
    ILogger<StoreController> logger,
    IStoreService storeService) : InternalControllerBase
{
    private readonly ILogger<StoreController> _logger = logger;
    private readonly IStoreService _storeService = storeService;

    /// <summary>
    /// Lists every store.
    /// </summary>
    /// <returns>The stores.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<StoreDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StoreList", OperationId = "StoreList")]
    public async Task<ActionResult<List<StoreDTO>>> StoreListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StoreList");

        var apiResponse = await _storeService.ListAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Creates, replaces or renames a store.
    /// </summary>
    /// <param name="item">The store data.</param>
    /// <returns>The saved name.</returns>
    [HttpPost]
    [ProducesResponseType<StoreSaveResponse>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StoreSave", OperationId = "StoreSave")]
    public async Task<ActionResult<StoreSaveResponse>> StoreSaveAsync(StoreSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StoreSave [Name={Name}] [OriginalName={OriginalName}]", item.Name, item.OriginalName);

        var apiResponse = await _storeService.SaveAsync(item);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Deletes a store.
    /// </summary>
    /// <param name="identifier">The store name.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StoreDelete", OperationId = "StoreDelete")]
    public async Task<ActionResult<string>> StoreDeleteAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StoreDelete [Identifier={Identifier}]", identifier);

        var apiResponse = await _storeService.DeleteAsync(identifier);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Turns a store on or off.
    /// </summary>
    /// <param name="item">What to change.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("enable")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "StoreEnable", OperationId = "StoreEnable")]
    public async Task<ActionResult<string>> StoreEnableAsync(StoreEnableRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StoreEnable [Name={Name}] [Enabled={Enabled}]", item.Name, item.Enabled);

        var apiResponse = await _storeService.EnableAsync(item);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Runs one search on a store configuration and explains each listing per task.
    /// </summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The listings with their outcomes.</returns>
    [HttpPost("probe")]
    [ProducesResponseType<List<StoreProbeResult>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StoreProbe", OperationId = "StoreProbe")]
    public async Task<ActionResult<List<StoreProbeResult>>> StoreProbeAsync(StoreProbeRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StoreProbe [Name={Name}] [Query={Query}]", item.Store.Name, item.Query);

        var apiResponse = await _storeService.ProbeAsync(item);
        return apiResponse.ToActionResult();
    }
}
