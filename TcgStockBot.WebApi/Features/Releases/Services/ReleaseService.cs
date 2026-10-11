using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Releases.Models.DTOs;
using TcgStockBot.WebApi.Features.Releases.Models.Entities;
using TcgStockBot.WebApi.Features.Releases.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;

namespace TcgStockBot.WebApi.Features.Releases.Services;

/// <summary>
/// The launch calendar: what comes out when, and which task watches it.
/// </summary>
/// <param name="releaseRepository">releases.json.</param>
/// <param name="stockTaskRepository">The tasks (to check the link).</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class ReleaseService(
    IReleaseRepository releaseRepository,
    IStockTaskRepository stockTaskRepository,
    IGlobalizationHelper globalizationHelper) : IReleaseService
{
    private const string FieldName = "Name";
    private const string FieldReleaseDate = "ReleaseDate";
    private const string FieldPreOrderDate = "PreOrderDate";
    private const string FieldUrl = "Url";
    private const string FieldStockTaskId = "StockTaskId";
    private const string FieldIdentifier = "Identifier";

    private readonly IReleaseRepository _releaseRepository = releaseRepository;
    private readonly IStockTaskRepository _stockTaskRepository = stockTaskRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<ReleaseDTO>>> ListAsync() =>
        ServiceApiResponse<List<ReleaseDTO>>.Success(Sorted(await _releaseRepository.ListAsync()).Select(ToDTO).ToList());

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ReleaseDTO>> SaveAsync(ReleaseSaveRequest item)
    {
        var releases = await _releaseRepository.ListAsync();
        var id = (item.Id ?? string.Empty).Trim();
        var existing = id.Length == 0 ? null : releases.FirstOrDefault(release => release.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (id.Length > 0 && existing is null)
        {
            return ServiceApiResponse<ReleaseDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), id));
        }

        var validationErrors = await ValidateAsync(item);
        if (validationErrors.Count > 0)
        {
            return ServiceApiResponse<ReleaseDTO>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)))
                .AddErrorRange(validationErrors);
        }

        var release = existing ?? new Release { Id = Guid.NewGuid().ToString("N")[..12] };
        release.Name = item.Name!.Trim();
        release.Game = item.Game;
        release.ReleaseDate = item.ReleaseDate!.Value;
        release.PreOrderDate = item.PreOrderDate;
        release.Url = Blank(item.Url);
        release.Notes = Blank(item.Notes);
        release.StockTaskId = Blank(item.StockTaskId);
        release.Aliases = [.. (item.Aliases ?? []).Select(static alias => alias.Trim()).Where(static alias => alias.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

        if (existing is null)
        {
            releases.Add(release);
        }

        await _releaseRepository.SaveAsync(Sorted(releases));
        return ServiceApiResponse<ReleaseDTO>.Success(ToDTO(release));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return ServiceApiResponse<string>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)))
                .AddErrorRange([await _globalizationHelper.ValidationErrorAsync(FieldIdentifier, nameof(TranslationKeys.RequiredField))]);
        }

        var releases = await _releaseRepository.ListAsync();
        if (releases.RemoveAll(release => release.Id.Equals(identifier.Trim(), StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return ServiceApiResponse<string>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier));
        }

        await _releaseRepository.SaveAsync(releases);
        return ServiceApiResponse<string>.Success();
    }

    /// <summary>
    /// Soonest first; same day by name.
    /// </summary>
    /// <param name="releases">The releases.</param>
    /// <returns>A sorted copy.</returns>
    internal static List<Release> Sorted(IEnumerable<Release> releases) =>
        releases.OrderBy(static release => release.ReleaseDate).ThenBy(static release => release.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private async Task<List<ApiValidationError>> ValidateAsync(ReleaseSaveRequest item)
    {
        var validationErrors = new List<ApiValidationError>();

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.RequiredField)));
        }

        if (item.ReleaseDate is null)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldReleaseDate, nameof(TranslationKeys.RequiredField)));
        }
        else if (item.PreOrderDate is { } preOrder && preOrder > item.ReleaseDate.Value)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldPreOrderDate, nameof(TranslationKeys.PreOrderAfterRelease)));
        }

        if (Blank(item.Url) is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUrl, nameof(TranslationKeys.InvalidUrl)));
        }

        if (Blank(item.StockTaskId) is { } taskId && await _stockTaskRepository.GetAsync(taskId) is null)
        {
            validationErrors.Add(new ApiValidationError(FieldStockTaskId, await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), taskId)));
        }

        return validationErrors;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ReleaseDTO ToDTO(Release release) => new()
    {
        Id = release.Id,
        Name = release.Name,
        Game = release.Game,
        ReleaseDate = release.ReleaseDate,
        PreOrderDate = release.PreOrderDate,
        Url = release.Url,
        Notes = release.Notes,
        StockTaskId = release.StockTaskId,
        Aliases = [.. release.Aliases],
    };
}
