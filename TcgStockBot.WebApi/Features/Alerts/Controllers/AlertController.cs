using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Alerts.Models.DTOs;
using TcgStockBot.WebApi.Features.Alerts.Services;

namespace TcgStockBot.WebApi.Features.Alerts.Controllers;

/// <summary>
/// Sent alerts.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="alertService">Alert business logic.</param>
[Authorize]
[Route("alert")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class AlertController(
    ILogger<AlertController> logger,
    IAlertService alertService) : InternalControllerBase
{
    private readonly ILogger<AlertController> _logger = logger;
    private readonly IAlertService _alertService = alertService;

    /// <summary>
    /// Lists the alert history, newest first.
    /// </summary>
    /// <returns>The alerts.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<AlertDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AlertList", OperationId = "AlertList")]
    public async Task<ActionResult<List<AlertDTO>>> AlertListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AlertList");

        var apiResponse = await _alertService.ListAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Forgets every alerted product, so everything in stock alerts again next cycle.
    /// </summary>
    /// <returns>No content on success.</returns>
    [HttpPost("reset")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AlertReset", OperationId = "AlertReset")]
    public async Task<ActionResult<string>> AlertResetAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AlertReset");

        var apiResponse = await _alertService.ResetAsync();
        return apiResponse.ToActionResult();
    }
}
