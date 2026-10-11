using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Releases.Models.DTOs;
using TcgStockBot.WebApi.Features.Releases.Services;

namespace TcgStockBot.WebApi.Features.Releases.Controllers;

/// <summary>
/// The launch calendar (Lançamentos).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="releaseService">Release business logic.</param>
[Authorize]
[Route("release")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class ReleaseController(
    ILogger<ReleaseController> logger,
    IReleaseService releaseService) : InternalControllerBase
{
    private readonly ILogger<ReleaseController> _logger = logger;
    private readonly IReleaseService _releaseService = releaseService;

    /// <summary>
    /// Lists every release, soonest first.
    /// </summary>
    /// <returns>The releases.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<ReleaseDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ReleaseList", OperationId = "ReleaseList")]
    public async Task<ActionResult<List<ReleaseDTO>>> ReleaseListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("ReleaseList");

        var apiResponse = await _releaseService.ListAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Creates (empty id) or replaces a release.
    /// </summary>
    /// <param name="item">The release.</param>
    /// <returns>The saved release.</returns>
    [HttpPost]
    [ProducesResponseType<ReleaseDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "ReleaseSave", OperationId = "ReleaseSave")]
    public async Task<ActionResult<ReleaseDTO>> ReleaseSaveAsync(ReleaseSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("ReleaseSave [Identifier={Identifier}]", item.Id);

        var apiResponse = await _releaseService.SaveAsync(item);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Deletes a release.
    /// </summary>
    /// <param name="identifier">The release identifier.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "ReleaseDelete", OperationId = "ReleaseDelete")]
    public async Task<ActionResult<string>> ReleaseDeleteAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("ReleaseDelete [Identifier={Identifier}]", identifier);

        var apiResponse = await _releaseService.DeleteAsync(identifier);
        return apiResponse.ToActionResult();
    }
}
