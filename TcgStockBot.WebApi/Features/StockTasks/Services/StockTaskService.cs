using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;

namespace TcgStockBot.WebApi.Features.StockTasks.Services;

/// <summary>
/// Business logic for stock tasks: validation, then the repository.
/// </summary>
/// <param name="stockTaskRepository">Task data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class StockTaskService(
    IStockTaskRepository stockTaskRepository,
    IGlobalizationHelper globalizationHelper) : IStockTaskService
{
    private const string FieldIdentifier = "Identifier";
    private const string FieldName = "Name";
    private const string FieldSearchQuery = "SearchQuery";
    private const string FieldMaxPrice = "MaxPrice";

    private readonly IStockTaskRepository _stockTaskRepository = stockTaskRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<StockTaskDTO>>> ListAsync()
    {
        var tasks = await _stockTaskRepository.ListAsync();
        return ServiceApiResponse<List<StockTaskDTO>>.Success(tasks.Select(static task => new StockTaskDTO(task)).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<StockTaskDTO>> GetAsync(string identifier)
    {
        var validationErrors = await ValidateIdentifierAsync(identifier);
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<StockTaskDTO>()).AddErrorRange(validationErrors);
        }

        var task = await _stockTaskRepository.GetAsync(identifier);
        return task is null
            ? ServiceApiResponse<StockTaskDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier))
            : ServiceApiResponse<StockTaskDTO>.Success(new StockTaskDTO(task));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<StockTaskSaveResponse>> SaveAsync(StockTaskSaveRequest item)
    {
        var validationErrors = await ValidateSaveAsync(item);
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<StockTaskSaveResponse>()).AddErrorRange(validationErrors);
        }

        var saved = await _stockTaskRepository.SaveAsync(item.StockTaskNew());
        return ServiceApiResponse<StockTaskSaveResponse>.Success(new StockTaskSaveResponse { Id = saved.Id });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string identifier)
    {
        var validationErrors = await ValidateIdentifierAsync(identifier);
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<string>()).AddErrorRange(validationErrors);
        }

        await _stockTaskRepository.DeleteAsync(identifier);
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> EnableAsync(StockTaskEnableRequest item)
    {
        if (!string.IsNullOrWhiteSpace(item.Id) && await _stockTaskRepository.GetAsync(item.Id) is null)
        {
            return ServiceApiResponse<string>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Id));
        }

        await _stockTaskRepository.SetEnabledAsync(string.IsNullOrWhiteSpace(item.Id) ? null : item.Id, item.Enabled);
        return ServiceApiResponse<string>.Success();
    }

    private async Task<List<ApiValidationError>> ValidateIdentifierAsync(string identifier)
    {
        var validationErrors = new List<ApiValidationError>();

        if (string.IsNullOrWhiteSpace(identifier))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.RequiredField)));
        }

        return validationErrors;
    }

    private async Task<List<ApiValidationError>> ValidateSaveAsync(StockTaskSaveRequest item)
    {
        var validationErrors = new List<ApiValidationError>();
        var identifier = item.Id?.Trim() ?? string.Empty;

        if (identifier.Length == 0)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.RequiredField)));
        }
        else if (identifier.Contains('|'))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.InvalidCharacter)));
        }
        else
        {
            var existing = await _stockTaskRepository.GetAsync(identifier);
            if (item.IsNew && existing is not null)
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.AlreadyExists), identifier));
            }
            else if (!item.IsNew && existing is null)
            {
                validationErrors.Add(new ApiValidationError(FieldIdentifier,
                    await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier)));
            }
        }

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.RequiredField)));
        }

        if (string.IsNullOrWhiteSpace(item.SearchQuery))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldSearchQuery, nameof(TranslationKeys.RequiredField)));
        }

        if (item.MaxPrice <= 0)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldMaxPrice, nameof(TranslationKeys.GreaterThen0)));
        }

        return validationErrors;
    }

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));
}
