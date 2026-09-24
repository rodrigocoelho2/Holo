using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Settings.Models.DTOs;
using TcgStockBot.WebApi.Features.Settings.Services;

namespace TcgStockBot.WebApi.Features.Settings.Controllers;

/// <summary>
/// Monitoring settings (frequency, pauses, User-Agent).
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="settingService">Settings business logic.</param>
[Authorize]
[Route("setting")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class SettingController(
    ILogger<SettingController> logger,
    ISettingService settingService) : InternalControllerBase
{
    private readonly ILogger<SettingController> _logger = logger;
    private readonly ISettingService _settingService = settingService;

    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    [HttpGet]
    [ProducesResponseType<SettingDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SettingGet", OperationId = "SettingGet")]
    public async Task<ActionResult<SettingDTO>> SettingGetAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("SettingGet");

        var apiResponse = await _settingService.GetAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="item">The new settings.</param>
    /// <returns>The saved settings.</returns>
    [HttpPost]
    [ProducesResponseType<SettingDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SettingSave", OperationId = "SettingSave")]
    public async Task<ActionResult<SettingDTO>> SettingSaveAsync(SettingSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("SettingSave [PollIntervalSeconds={PollIntervalSeconds}]", item.PollIntervalSeconds);

        var apiResponse = await _settingService.SaveAsync(item);
        return apiResponse.ToActionResult();
    }
}
