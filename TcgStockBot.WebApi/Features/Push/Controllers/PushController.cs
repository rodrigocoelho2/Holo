using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Push.Models.DTOs;
using TcgStockBot.WebApi.Features.Push.Services;

namespace TcgStockBot.WebApi.Features.Push.Controllers;

/// <summary>
/// Push notifications: the public key browsers subscribe with, and the subscribed devices.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="pushService">Push business logic.</param>
[Authorize]
[Route("push")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PushController(
    ILogger<PushController> logger,
    IPushService pushService) : InternalControllerBase
{
    private readonly ILogger<PushController> _logger = logger;
    private readonly IPushService _pushService = pushService;

    /// <summary>
    /// Gets the push state: on or off, the public key and the devices.
    /// </summary>
    /// <returns>The state.</returns>
    [HttpGet]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushGet", OperationId = "PushGet")]
    public async Task<ActionResult<PushDTO>> PushGetAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PushGet");

        var apiResponse = await _pushService.GetAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Adds a device (or updates it).
    /// </summary>
    /// <param name="item">The browser's subscription.</param>
    /// <returns>The device identifier.</returns>
    [HttpPost("subscribe")]
    [ProducesResponseType<PushSubscribeResponse>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushSubscribe", OperationId = "PushSubscribe")]
    public async Task<ActionResult<PushSubscribeResponse>> PushSubscribeAsync(PushSubscribeRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PushSubscribe");

        var apiResponse = await _pushService.SubscribeAsync(item);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Removes a device.
    /// </summary>
    /// <param name="item">The device.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("unsubscribe")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushUnsubscribe", OperationId = "PushUnsubscribe")]
    public async Task<ActionResult<string>> PushUnsubscribeAsync(PushUnsubscribeRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PushUnsubscribe");

        var apiResponse = await _pushService.UnsubscribeAsync(item);
        return apiResponse.ToActionResult();
    }
}
