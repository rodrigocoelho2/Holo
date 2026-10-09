using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Text;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.StockTasks.Repositories;
using TcgStockBot.WebApi.Features.StockTasks.Services;
using TcgStockBot.WebApi.Features.Stores.Models.DTOs;
using TcgStockBot.WebApi.Features.Stores.Repositories;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Business logic for stores: validation, keeping tasks consistent on rename/delete, and store probing.
/// </summary>
/// <param name="storeRepository">Store data access.</param>
/// <param name="stockTaskRepository">Task data access (store references).</param>
/// <param name="storeAdapterResolver">Picks the platform adapter.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class StoreService(
    IStoreRepository storeRepository,
    IStockTaskRepository stockTaskRepository,
    IStoreAdapterResolver storeAdapterResolver,
    IGlobalizationHelper globalizationHelper) : IStoreService
{
    private const string FieldName = "Name";
    private const string FieldBaseUrl = "BaseUrl";
    private const string FieldSearchUrlTemplate = "SearchUrlTemplate";
    private const string FieldShopifyCatalogPaths = "ShopifyCatalogPaths";
    private const string FieldAwinFeedKey = "AwinFeedKey";
    private const string FieldProductSelector = "ProductSelector";
    private const string FieldQuery = "Query";

    private readonly IStoreRepository _storeRepository = storeRepository;
    private readonly IStockTaskRepository _stockTaskRepository = stockTaskRepository;
    private readonly IStoreAdapterResolver _storeAdapterResolver = storeAdapterResolver;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<StoreDTO>>> ListAsync()
    {
        var stores = await _storeRepository.ListAsync();
        return ServiceApiResponse<List<StoreDTO>>.Success(stores.Select(static store => new StoreDTO(store)).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<StoreSaveResponse>> SaveAsync(StoreSaveRequest item)
    {
        var validationErrors = await ValidateSaveAsync(item);
        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<StoreSaveResponse>()).AddErrorRange(validationErrors);
        }

        var store = item.StoreNew();
        var existing = item.OriginalName is null ? null : await _storeRepository.GetAsync(item.OriginalName);
        if (existing is not null)
        {
            // Health is the bot's, not the form's: keep it, and start counting again when the store is turned back on.
            store.LastTest = existing.LastTest;
            store.ConsecutiveFailures = store.Enabled && !existing.Enabled ? 0 : existing.ConsecutiveFailures;
            store.AutoDisabledAt = store.Enabled ? null : existing.AutoDisabledAt;
        }

        await _storeRepository.SaveAsync(store, item.OriginalName);

        if (item.OriginalName is not null && !item.OriginalName.Equals(store.Name, StringComparison.OrdinalIgnoreCase))
        {
            await _stockTaskRepository.RenameStoreAsync(item.OriginalName, store.Name);
        }

        return ServiceApiResponse<StoreSaveResponse>.Success(new StoreSaveResponse { Name = store.Name });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (await ValidationProblemsAsync<string>())
                .AddErrorRange([await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.RequiredField))]);
        }

        await _storeRepository.DeleteAsync(name);
        await _stockTaskRepository.RemoveStoreAsync(name);
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> EnableAsync(StoreEnableRequest item)
    {
        if (await _storeRepository.GetAsync(item.Name) is null)
        {
            return ServiceApiResponse<string>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Name));
        }

        await _storeRepository.SetEnabledAsync(item.Name, item.Enabled);
        if (item.Enabled)
        {
            // Turned back on by hand: a fresh start for the failed-cycle count.
            await _storeRepository.UpdateAsync(stores =>
            {
                foreach (var store in stores.Where(store => store.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    store.ConsecutiveFailures = 0;
                    store.AutoDisabledAt = null;
                    store.Tags.RemoveAll(static tag => tag.Equals(StoreHealthService.FailingTag, StringComparison.OrdinalIgnoreCase));
                }
            });
        }

        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<StoreProbeResult>>> ProbeAsync(StoreProbeRequest item)
    {
        var validationErrors = await ValidateStoreAsync(item.Store);
        if (string.IsNullOrWhiteSpace(item.Query))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldQuery, nameof(TranslationKeys.RequiredField)));
        }

        if (validationErrors.Count > 0)
        {
            return (await ValidationProblemsAsync<List<StoreProbeResult>>()).AddErrorRange(validationErrors);
        }

        var store = item.Store.StoreNew();
        var tasks = await _stockTaskRepository.ListAsync();

        try
        {
            var listings = await _storeAdapterResolver.Resolve(store).SearchAsync(store, item.Query.Trim(), CancellationToken.None);

            return ServiceApiResponse<List<StoreProbeResult>>.Success(listings.Select(listing => new StoreProbeResult
            {
                Title = listing.Title,
                Url = listing.Url.AbsoluteUri,
                Price = listing.Price,
                InStock = listing.InStock,
                Language = LanguageClassifier.Classify(listing.Title),
                OutcomeByTask = tasks.ToDictionary(task => task.Id, task => StockTaskMatcher.Evaluate(task, listing, store.UnmarkedLanguage)),
                ProductId = listing.ProductId,
                Currency = listing.Currency,
                Seller = listing.Seller,
                PreOrder = listing.PreOrder || PreOrderDetector.IsPreOrder(listing.Title),
            }).ToList());
        }
        catch (Exception exception)
        {
            // Any failure (store blocking us, unexpected JSON/HTML, bad data) is reported to the dashboard, never a 500.
            var message = await StoreFailure.DescribeAsync(exception, _globalizationHelper);
            return ServiceApiResponse<List<StoreProbeResult>>.ValidationProblems(message).AddError(FieldBaseUrl, message);
        }
    }

    private async Task<List<ApiValidationError>> ValidateSaveAsync(StoreSaveRequest item)
    {
        var validationErrors = await ValidateStoreAsync(item);
        var name = (item.Name ?? string.Empty).Trim();

        if (item.OriginalName is not null && await _storeRepository.GetAsync(item.OriginalName) is null)
        {
            validationErrors.Add(new ApiValidationError(FieldName,
                await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.OriginalName)));
        }

        var isRenameOrNew = item.OriginalName is null || !item.OriginalName.Equals(name, StringComparison.OrdinalIgnoreCase);
        if (name.Length > 0 && isRenameOrNew && await _storeRepository.GetAsync(name) is not null)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.AlreadyExists), name));
        }

        return validationErrors;
    }

    /// <summary>Checks the fields every store needs to be searchable (shared by Save and Probe).</summary>
    private async Task<List<ApiValidationError>> ValidateStoreAsync(StoreSaveRequest item)
    {
        var validationErrors = new List<ApiValidationError>();
        var name = (item.Name ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.RequiredField)));
        }
        else if (name.Contains('|'))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldName, nameof(TranslationKeys.InvalidCharacter)));
        }

        if (!IsHttpUrl(item.BaseUrl))
        {
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldBaseUrl, nameof(TranslationKeys.InvalidUrl)));
        }

        if (item.Platform == StorePlatform.Shopify && !string.IsNullOrWhiteSpace(item.ShopifyCatalogPaths)
            && item.ShopifyCatalogPaths.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static path => !path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal) || path.Contains('?')))
        {
            // Paths on the store itself only ("/collections/pokemon"): never another host.
            validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldShopifyCatalogPaths, nameof(TranslationKeys.InvalidUrl)));
        }

        if (item.Platform == StorePlatform.Awin)
        {
            // The key becomes part of a configuration name (AwinFeeds__Feeds__{key}): letters, digits and '_' only.
            var feedKey = (item.AwinFeedKey ?? string.Empty).Trim();
            if (feedKey.Length == 0)
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldAwinFeedKey, nameof(TranslationKeys.RequiredField)));
            }
            else if (!feedKey.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldAwinFeedKey, nameof(TranslationKeys.InvalidKey)));
            }
        }

        if (item.Platform == StorePlatform.Html)
        {
            if (item.Html is null || !IsHttpUrl(item.Html.SearchUrlTemplate?.Replace("{query}", "q", StringComparison.Ordinal)))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldSearchUrlTemplate, nameof(TranslationKeys.InvalidUrl)));
            }

            if (item.Html is null || string.IsNullOrWhiteSpace(item.Html.ProductSelector))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldProductSelector, nameof(TranslationKeys.RequiredField)));
            }
        }

        return validationErrors;
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));
}
