using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Settings.Models.DTOs;
using TcgStockBot.WebApi.Features.Settings.Repositories;

namespace TcgStockBot.WebApi.Features.Settings.Services;

/// <summary>
/// Business logic for the monitoring settings.
/// </summary>
/// <param name="settingRepository">Settings data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class SettingService(
    ISettingRepository settingRepository,
    IGlobalizationHelper globalizationHelper) : ISettingService
{
    private const int MinimumPollIntervalSeconds = 60;
    private const string FieldPollInterval = "PollIntervalSeconds";
    private const string FieldPollJitter = "PollJitterSeconds";
    private const string FieldDelayBetweenRequests = "DelayBetweenRequestsSeconds";
    private const string FieldUserAgent = "UserAgent";

    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingDTO>> GetAsync() =>
        ServiceApiResponse<SettingDTO>.Success(new SettingDTO(await _settingRepository.GetAsync()));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingDTO>> SaveAsync(SettingSaveRequest item)
    {
        var validationErrors = await ValidateSaveAsync(item);
        if (validationErrors.Count > 0)
        {
            return ServiceApiResponse<SettingDTO>
                .ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)))
                .AddErrorRange(validationErrors);
        }

        var current = await _settingRepository.GetAsync();
        var saved = await _settingRepository.SaveAsync(item.SettingNew(current.Paused));
        return ServiceApiResponse<SettingDTO>.Success(new SettingDTO(saved));
    }

    private async Task<List<ApiValidationError>> ValidateSaveAsync(SettingSaveRequest item)
    {
        var validationErrors = new List<ApiValidationError>();

        if (item.PollIntervalSeconds < MinimumPollIntervalSeconds)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldPollInterval, nameof(TranslationKeys.PollIntervalTooShort)));
        }

        if (item.PollJitterSeconds < 0)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldPollJitter, nameof(TranslationKeys.NegativeValue)));
        }

        if (item.DelayBetweenRequestsSeconds < 0)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldDelayBetweenRequests, nameof(TranslationKeys.NegativeValue)));
        }

        if (string.IsNullOrWhiteSpace(item.UserAgent))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserAgent, nameof(TranslationKeys.RequiredField)));
        }

        return validationErrors;
    }
}
