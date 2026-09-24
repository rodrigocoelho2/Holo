using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Notifications.Models.DTOs;
using TcgStockBot.WebApi.Features.Notifications.Services;

namespace TcgStockBot.WebApi.Features.Notifications.Controllers;

/// <summary>
/// Alert delivery channels (email, WhatsApp).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="notificationService">Notification business logic.</param>
[Authorize]
[Route("notification")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class NotificationController(
    ILogger<NotificationController> logger,
    INotificationService notificationService) : InternalControllerBase
{
    private readonly ILogger<NotificationController> _logger = logger;
    private readonly INotificationService _notificationService = notificationService;

    /// <summary>
    /// Gets the state of each channel.
    /// </summary>
    /// <returns>The channel states.</returns>
    [HttpGet]
    [ProducesResponseType<NotificationDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "NotificationGet", OperationId = "NotificationGet")]
    public async Task<ActionResult<NotificationDTO>> NotificationGetAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("NotificationGet");

        var apiResponse = await _notificationService.GetAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Sends a sample alert through one channel.
    /// </summary>
    /// <param name="item">The channel to test.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("test")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "NotificationTest", OperationId = "NotificationTest")]
    public async Task<ActionResult<string>> NotificationTestAsync(NotificationTestRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("NotificationTest [Channel={Channel}]", item.Channel);

        var apiResponse = await _notificationService.TestAsync(item);
        return apiResponse.ToActionResult();
    }
}
