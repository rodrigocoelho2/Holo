using TcgStockBot.Library.Validation;
using TcgStockBot.Sdk;

namespace TcgStockBot.Website.Model;

/// <summary>
/// Base of every Website model: holds the API client and turns API failures into <see cref="AjaxResult"/> errors.
/// Models do the work; controllers only create them and return their result.
/// </summary>
/// <param name="apiClient">The TcgStockBot.WebApi client.</param>
public abstract class WebsiteBaseModel(TcgStockBotClient apiClient)
{
    private const string ApiUnavailableMessage = "Can't reach the bot (WebApi). Check that the tcgstockbot-webapi container is running.";

    /// <summary>Gets the API client.</summary>
    protected TcgStockBotClient ApiClient { get; } = apiClient;

    /// <summary>
    /// Calls the API and wraps the result.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="call">The API call.</param>
    /// <returns>The data, or the validation errors.</returns>
    protected static async Task<AjaxDataResult<T>> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return new AjaxDataResult<T> { Data = await call() };
        }
        catch (TcgStockBotApiException exception)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [.. exception.ToValidationErrors()] };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new AjaxDataResult<T> { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, ApiUnavailableMessage)] };
        }
    }

    /// <summary>
    /// Calls an API operation that returns no data.
    /// </summary>
    /// <param name="call">The API call.</param>
    /// <returns>Success, or the validation errors.</returns>
    protected static async Task<AjaxDataResult<bool>> CallAsync(Func<Task> call) =>
        await CallAsync(async () =>
        {
            await call();
            return true;
        });
}
