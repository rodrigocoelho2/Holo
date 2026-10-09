using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Compares.Models.DTOs;
using TcgStockBot.WebApi.Features.Compares.Services;

namespace TcgStockBot.WebApi.Features.Compares.Controllers;

/// <summary>
/// The same product across stores: cheapest now, lowest ever and recent drops.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="compareService">Compare business logic.</param>
[Authorize]
[Route("compare")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class CompareController(
    ILogger<CompareController> logger,
    ICompareService compareService) : InternalControllerBase
{
    private readonly ILogger<CompareController> _logger = logger;
    private readonly ICompareService _compareService = compareService;

    /// <summary>
    /// Lists every compared product (without price points).
    /// </summary>
    /// <returns>The groups.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<CompareGroupDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "CompareList", OperationId = "CompareList")]
    public async Task<ActionResult<List<CompareGroupDTO>>> CompareListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("CompareList");

        var apiResponse = await _compareService.ListAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Gets one compared product with every store's price points.
    /// </summary>
    /// <param name="identifier">The group key.</param>
    /// <returns>The group.</returns>
    [HttpGet]
    [ProducesResponseType<CompareGroupDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "CompareGet", OperationId = "CompareGet")]
    public async Task<ActionResult<CompareGroupDTO>> CompareGetAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("CompareGet");

        var apiResponse = await _compareService.GetAsync(identifier);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Moves one listing to another product, to a product of its own ("solo"), or back to automatic (empty).
    /// </summary>
    /// <param name="item">The listing and its target.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("assign")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "CompareAssign", OperationId = "CompareAssign")]
    public async Task<ActionResult<string>> CompareAssignAsync(CompareAssignRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("CompareAssign");

        var apiResponse = await _compareService.AssignAsync(item);
        return apiResponse.ToActionResult();
    }
}
