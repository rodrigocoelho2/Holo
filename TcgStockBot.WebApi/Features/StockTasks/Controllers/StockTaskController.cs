using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Mvc;
using TcgStockBot.WebApi.Extensions.Services;
using TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;
using TcgStockBot.WebApi.Features.StockTasks.Services;

namespace TcgStockBot.WebApi.Features.StockTasks.Controllers;

/// <summary>
/// Stock tasks: the products the bot watches.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="stockTaskService">Task business logic.</param>
[Authorize]
[Route("stocktask")]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class StockTaskController(
    ILogger<StockTaskController> logger,
    IStockTaskService stockTaskService) : InternalControllerBase
{
    private readonly ILogger<StockTaskController> _logger = logger;
    private readonly IStockTaskService _stockTaskService = stockTaskService;

    /// <summary>
    /// Lists every stock task.
    /// </summary>
    /// <returns>The tasks.</returns>
    [HttpGet("list")]
    [ProducesResponseType<List<StockTaskDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StockTaskList", OperationId = "StockTaskList")]
    public async Task<ActionResult<List<StockTaskDTO>>> StockTaskListAsync()
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StockTaskList");

        var apiResponse = await _stockTaskService.ListAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Gets one stock task.
    /// </summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>The task.</returns>
    [HttpGet]
    [ProducesResponseType<StockTaskDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "StockTaskGet", OperationId = "StockTaskGet")]
    public async Task<ActionResult<StockTaskDTO>> StockTaskGetAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StockTaskGet [Identifier={Identifier}]", identifier);

        var apiResponse = await _stockTaskService.GetAsync(identifier);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Creates or replaces a stock task.
    /// </summary>
    /// <param name="item">The task data.</param>
    /// <returns>The saved identifier.</returns>
    [HttpPost]
    [ProducesResponseType<StockTaskSaveResponse>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StockTaskSave", OperationId = "StockTaskSave")]
    public async Task<ActionResult<StockTaskSaveResponse>> StockTaskSaveAsync(StockTaskSaveRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StockTaskSave [Identifier={Identifier}] [IsNew={IsNew}]", item.Id, item.IsNew);

        var apiResponse = await _stockTaskService.SaveAsync(item);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Deletes a stock task.
    /// </summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "StockTaskDelete", OperationId = "StockTaskDelete")]
    public async Task<ActionResult<string>> StockTaskDeleteAsync(string identifier)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StockTaskDelete [Identifier={Identifier}]", identifier);

        var apiResponse = await _stockTaskService.DeleteAsync(identifier);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Starts or stops one stock task, or all of them.
    /// </summary>
    /// <param name="item">What to change.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("enable")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "StockTaskEnable", OperationId = "StockTaskEnable")]
    public async Task<ActionResult<string>> StockTaskEnableAsync(StockTaskEnableRequest item)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("StockTaskEnable [Identifier={Identifier}] [Enabled={Enabled}]", item.Id, item.Enabled);

        var apiResponse = await _stockTaskService.EnableAsync(item);
        return apiResponse.ToActionResult();
    }
}
