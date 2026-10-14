using System.Net;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Extensions.Http;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.WebApi.Features.Stores.Services;

/// <summary>
/// Turns a failed store request into a short, translated explanation.
/// </summary>
internal static class StoreFailure
{
    /// <summary>
    /// Describes why a store search failed.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <param name="globalizationHelper">Translations.</param>
    /// <returns>"The store refused the request (HTTP 403)…" and similar.</returns>
    public static Task<string> DescribeAsync(Exception exception, IGlobalizationHelper globalizationHelper) => exception switch
    {
        StoreWithoutIntegrationException manual =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreWithoutIntegration), manual.StoreName),
        StoreIntegrationException { Problem: StoreIntegrationProblem.NotConfigured } integration =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreIntegrationNotConfigured), integration.Provider, integration.Detail),
        StoreIntegrationException { Problem: StoreIntegrationProblem.Unauthorized } integration =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreIntegrationUnauthorized), integration.Provider, integration.Detail),
        StoreIntegrationException integration =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreIntegrationApiError), integration.Provider, integration.Detail),
        OutboundThrottledException throttled =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreThrottled), throttled.Host, (int)throttled.RetryAfter.TotalSeconds),
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreBlocked)),
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreRateLimited)),
        HttpRequestException { StatusCode: { } status } when (int)status >= 500 =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreServerError), (int)status),
        HttpRequestException { StatusCode: { } status } =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreHttpError), (int)status, status),
        HttpRequestException { HttpRequestError: HttpRequestError.NameResolutionError } =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.DnsFailed)),
        HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError } =>
            globalizationHelper.TranslateAsync(nameof(TranslationKeys.ConnectionRefused)),
        TaskCanceledException => globalizationHelper.TranslateAsync(nameof(TranslationKeys.StoreTimeout)),
        _ => Task.FromResult($"{exception.GetType().Name}: {exception.Message}"),
    };
}
