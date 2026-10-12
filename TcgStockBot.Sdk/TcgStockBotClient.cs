using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TcgStockBot.Library.Validation;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Sdk;

/// <summary>
/// Typed client of TcgStockBot.WebApi. Method names follow the API operation ids ({Feature}{Operation}Async).
/// Register with <see cref="ServiceCollectionExtensions.AddTcgStockBotClient"/>, which sets the base address and the x-api-key header.
/// </summary>
/// <param name="httpClient">The configured HttpClient.</param>
public sealed class TcgStockBotClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    // ----- StockTask -----

    /// <summary>Lists every stock task.</summary>
    /// <returns>The tasks.</returns>
    public Task<List<StockTaskDTO>> StockTaskListAsync() => GetAsync<List<StockTaskDTO>>("stocktask/list");

    /// <summary>Gets one stock task.</summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>The task.</returns>
    public Task<StockTaskDTO> StockTaskGetAsync(string identifier) => GetAsync<StockTaskDTO>($"stocktask?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Creates or replaces a stock task.</summary>
    /// <param name="item">The task data.</param>
    /// <returns>The saved identifier.</returns>
    public Task<StockTaskSaveResponse> StockTaskSaveAsync(StockTaskSaveRequest item) => PostAsync<StockTaskSaveResponse>("stocktask", item);

    /// <summary>Deletes a stock task.</summary>
    /// <param name="identifier">The task identifier.</param>
    /// <returns>A task that completes on success.</returns>
    public Task StockTaskDeleteAsync(string identifier) => DeleteAsync($"stocktask?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Starts or stops one task, or every task.</summary>
    /// <param name="item">What to change.</param>
    /// <returns>A task that completes on success.</returns>
    public Task StockTaskEnableAsync(StockTaskEnableRequest item) => PostAsync("stocktask/enable", item);

    // ----- Store -----

    /// <summary>Lists every store.</summary>
    /// <returns>The stores.</returns>
    public Task<List<StoreDTO>> StoreListAsync() => GetAsync<List<StoreDTO>>("store/list");

    /// <summary>Creates, replaces or renames a store.</summary>
    /// <param name="item">The store data.</param>
    /// <returns>The saved name.</returns>
    public Task<StoreSaveResponse> StoreSaveAsync(StoreSaveRequest item) => PostAsync<StoreSaveResponse>("store", item);

    /// <summary>Deletes a store.</summary>
    /// <param name="identifier">The store name.</param>
    /// <returns>A task that completes on success.</returns>
    public Task StoreDeleteAsync(string identifier) => DeleteAsync($"store?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Detects how a store can be searched from its address.</summary>
    /// <param name="item">The store address.</param>
    /// <returns>The suggested configuration.</returns>
    public Task<StoreDetectResult> StoreDetectAsync(StoreDetectRequest item) => PostAsync<StoreDetectResult>("store/detect", item);

    /// <summary>Tests one saved store and keeps the result on it.</summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The test result.</returns>
    public Task<StoreTestResultDTO> StoreTestAsync(StoreTestRequest item) => PostAsync<StoreTestResultDTO>("store/test", item);

    /// <summary>Turns a store on or off.</summary>
    /// <param name="item">What to change.</param>
    /// <returns>A task that completes on success.</returns>
    public Task StoreEnableAsync(StoreEnableRequest item) => PostAsync("store/enable", item);

    /// <summary>Runs one search on a store configuration.</summary>
    /// <param name="item">The store and the search text.</param>
    /// <returns>The listings with their outcomes.</returns>
    public Task<List<StoreProbeResult>> StoreProbeAsync(StoreProbeRequest item) => PostAsync<List<StoreProbeResult>>("store/probe", item);

    // ----- Alert -----

    /// <summary>Lists the alert history, newest first.</summary>
    /// <returns>The alerts.</returns>
    public Task<List<AlertDTO>> AlertListAsync() => GetAsync<List<AlertDTO>>("alert/list");

    /// <summary>Forgets every alerted product.</summary>
    /// <returns>A task that completes on success.</returns>
    public Task AlertResetAsync() => PostAsync("alert/reset", null);

    /// <summary>Empties the alert list (the "already alerted" memory is kept).</summary>
    /// <returns>A task that completes on success.</returns>
    public Task AlertClearAsync() => PostAsync("alert/clear", null);

    // ----- Msrp -----

    /// <summary>The product kinds the bot recognises and the MSRP of each.</summary>
    /// <returns>The kinds and prices.</returns>
    public Task<MsrpListDTO> MsrpListAsync() => GetAsync<MsrpListDTO>("msrp/list");

    // ----- PriceHistory -----

    /// <summary>Lists every tracked product with its current, lowest and highest price.</summary>
    /// <returns>The summaries, most recently seen first.</returns>
    public Task<List<PriceHistorySummaryDTO>> PriceHistoryListAsync() => GetAsync<List<PriceHistorySummaryDTO>>("pricehistory/list");

    /// <summary>Gets the price history of one product.</summary>
    /// <param name="identifier">The history key.</param>
    /// <returns>The history.</returns>
    public Task<PriceHistoryDTO> PriceHistoryGetAsync(string identifier) => GetAsync<PriceHistoryDTO>($"pricehistory?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>When each store restocks, learnt from the history.</summary>
    /// <returns>One pattern per store.</returns>
    public Task<List<StoreRestockDTO>> PriceHistoryRestocksAsync() => GetAsync<List<StoreRestockDTO>>("pricehistory/restocks");

    // ----- Compare -----

    /// <summary>Lists every compared product (without price points).</summary>
    /// <returns>The groups.</returns>
    public Task<List<CompareGroupDTO>> CompareListAsync() => GetAsync<List<CompareGroupDTO>>("compare/list");

    /// <summary>Gets one compared product with every store's price points.</summary>
    /// <param name="identifier">The group key.</param>
    /// <returns>The group.</returns>
    public Task<CompareGroupDTO> CompareGetAsync(string identifier) => GetAsync<CompareGroupDTO>($"compare?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Corrects the grouping of one listing.</summary>
    /// <param name="item">The listing and its target group.</param>
    /// <returns>A task that completes on success.</returns>
    public Task CompareAssignAsync(CompareAssignRequest item) => PostAsync("compare/assign", item);

    // ----- User -----

    /// <summary>How many accounts exist.</summary>
    /// <returns>The status.</returns>
    public Task<UserStatusDTO> UserStatusAsync() => GetAsync<UserStatusDTO>("user/status");

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    public Task<List<UserDTO>> UserListAsync() => GetAsync<List<UserDTO>>("user/list");

    /// <summary>Gets one account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserGetAsync(string identifier) => GetAsync<UserDTO>($"user?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Creates an account.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserCreateAsync(UserCreateRequest item) => PostAsync<UserDTO>("user", item);

    /// <summary>Checks a user name and password.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserSignInAsync(UserSignInRequest item) => PostAsync<UserDTO>("user/signin", item);

    /// <summary>Changes a password.</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserPasswordAsync(UserPasswordRequest item) => PostAsync<UserDTO>("user/password", item);

    /// <summary>Deletes an account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>A task that completes on success.</returns>
    public Task UserDeleteAsync(string identifier) => DeleteAsync($"user?identifier={Uri.EscapeDataString(identifier)}");

    // ----- Release -----

    /// <summary>Lists every release, soonest first.</summary>
    /// <returns>The releases.</returns>
    public Task<List<ReleaseDTO>> ReleaseListAsync() => GetAsync<List<ReleaseDTO>>("release/list");

    /// <summary>Creates (empty id) or replaces a release.</summary>
    /// <param name="item">The release.</param>
    /// <returns>The saved release.</returns>
    public Task<ReleaseDTO> ReleaseSaveAsync(ReleaseSaveRequest item) => PostAsync<ReleaseDTO>("release", item);

    /// <summary>Deletes a release.</summary>
    /// <param name="identifier">The release identifier.</param>
    /// <returns>A task that completes on success.</returns>
    public Task ReleaseDeleteAsync(string identifier) => DeleteAsync($"release?identifier={Uri.EscapeDataString(identifier)}");

    // ----- Monitor -----

    /// <summary>Gets what the bot is doing right now.</summary>
    /// <returns>The monitor state.</returns>
    public Task<MonitorDTO> MonitorGetAsync() => GetAsync<MonitorDTO>("monitor");

    /// <summary>Asks for a check cycle now.</summary>
    /// <returns>A task that completes on success.</returns>
    public Task MonitorRunAsync() => PostAsync("monitor/run", null);

    /// <summary>Pauses scheduled cycles.</summary>
    /// <returns>A task that completes on success.</returns>
    public Task MonitorPauseAsync() => PostAsync("monitor/pause", null);

    /// <summary>Resumes scheduled cycles and checks immediately.</summary>
    /// <returns>A task that completes on success.</returns>
    public Task MonitorResumeAsync() => PostAsync("monitor/resume", null);

    // ----- Setting -----

    /// <summary>Gets the settings.</summary>
    /// <returns>The settings.</returns>
    public Task<SettingDTO> SettingGetAsync() => GetAsync<SettingDTO>("setting");

    /// <summary>Saves the settings.</summary>
    /// <param name="item">The new settings.</param>
    /// <returns>The saved settings.</returns>
    public Task<SettingDTO> SettingSaveAsync(SettingSaveRequest item) => PostAsync<SettingDTO>("setting", item);

    // ----- Notification -----

    /// <summary>Gets the state of each notification channel.</summary>
    /// <returns>The channel states.</returns>
    public Task<NotificationDTO> NotificationGetAsync() => GetAsync<NotificationDTO>("notification");

    /// <summary>Sends a sample alert through one channel.</summary>
    /// <param name="item">The channel to test.</param>
    /// <returns>A task that completes on success.</returns>
    public Task NotificationTestAsync(NotificationTestRequest item) => PostAsync("notification/test", item);

    // ----- Push -----

    /// <summary>Gets the push state: on or off, the public key and the devices.</summary>
    /// <returns>The state.</returns>
    public Task<PushDTO> PushGetAsync() => GetAsync<PushDTO>("push");

    /// <summary>Adds (or updates) a device that receives push notifications.</summary>
    /// <param name="item">The browser's subscription.</param>
    /// <returns>The device identifier.</returns>
    public Task<PushSubscribeResponse> PushSubscribeAsync(PushSubscribeRequest item) => PostAsync<PushSubscribeResponse>("push/subscribe", item);

    /// <summary>Removes a device.</summary>
    /// <param name="item">The device.</param>
    /// <returns>A task that completes on success.</returns>
    public Task PushUnsubscribeAsync(PushUnsubscribeRequest item) => PostAsync("push/unsubscribe", item);

    // ----- Transport -----

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await httpClient.GetAsync(path);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PostAsync<T>(string path, object? body)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, SerializerOptions);
        return await ReadAsync<T>(response);
    }

    private async Task PostAsync(string path, object? body)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, SerializerOptions);
        await EnsureSuccessAsync(response);
    }

    private async Task DeleteAsync(string path)
    {
        using var response = await httpClient.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(SerializerOptions))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiValidationProblemDetails? problems = null;
        try
        {
            problems = await response.Content.ReadFromJsonAsync<ApiValidationProblemDetails>(SerializerOptions);
        }
        catch (JsonException)
        {
            // The body is not a problem document (e.g. 401 with an empty body).
        }
        catch (NotSupportedException)
        {
            // Not JSON.
        }

        throw new TcgStockBotApiException(response.StatusCode, problems);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
