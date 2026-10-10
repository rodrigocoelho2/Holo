using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Msrps.Models.DTOs;
using TcgStockBot.WebApi.Features.Msrps.Services;

namespace TcgStockBot.WebApi.Features.Msrps.Controllers;

/// <summary>
/// MSRP reference prices and the product kinds the bot recognises.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="msrpService">MSRP service.</param>
[Authorize]
[Route("msrp")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class MsrpController(
    ILogger<MsrpController> logger,
    IMsrpService msrpService) : InternalControllerBase
{
    private readonly ILogger<MsrpController> _logger = logger;
    private readonly IMsrpService _msrpService = msrpService;

    /// <summary>
    /// The product kinds and the MSRP table.
    /// </summary>
    /// <returns>The kinds and prices.</returns>
    [HttpGet("list")]
    [ProducesResponseType<MsrpListDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "MsrpList", OperationId = "MsrpList")]
    public async Task<ActionResult<MsrpListDTO>> MsrpListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("MsrpList");

        var apiResponse = await _msrpService.ListAsync();
        return apiResponse.ToActionResult();
    }
}
