using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;
using TcgStockBot.WebApi.Features.Monitors.Services;

namespace TcgStockBot.WebApi.Features.Monitors.Controllers;

/// <summary>
/// The background monitor: live state, check now, pause and resume.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="monitorService">Monitor business logic.</param>
[Authorize]
[Route("monitor")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class MonitorController(
    ILogger<MonitorController> logger,
    IMonitorService monitorService) : InternalControllerBase
{
    private readonly ILogger<MonitorController> _logger = logger;
    private readonly IMonitorService _monitorService = monitorService;

    /// <summary>
    /// Gets what the bot is doing right now.
    /// </summary>
    /// <returns>The monitor state.</returns>
    [HttpGet]
    [ProducesResponseType<MonitorDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "MonitorGet", OperationId = "MonitorGet")]
    public async Task<ActionResult<MonitorDTO>> MonitorGetAsync()
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("MonitorGet");

        var apiResponse = await _monitorService.GetAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Asks for a check cycle now.
    /// </summary>
    /// <returns>No content on success.</returns>
    [HttpPost("run")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "MonitorRun", OperationId = "MonitorRun")]
    public async Task<ActionResult<string>> MonitorRunAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("MonitorRun");

        var apiResponse = await _monitorService.RunAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Pauses scheduled cycles.
    /// </summary>
    /// <returns>No content on success.</returns>
    [HttpPost("pause")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "MonitorPause", OperationId = "MonitorPause")]
    public async Task<ActionResult<string>> MonitorPauseAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("MonitorPause");

        var apiResponse = await _monitorService.PauseAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Resumes scheduled cycles and checks immediately.
    /// </summary>
    /// <returns>No content on success.</returns>
    [HttpPost("resume")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "MonitorResume", OperationId = "MonitorResume")]
    public async Task<ActionResult<string>> MonitorResumeAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("MonitorResume");

        var apiResponse = await _monitorService.ResumeAsync();
        return apiResponse.ToActionResult();
    }
}
