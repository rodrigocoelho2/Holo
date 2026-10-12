using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Push.Models;
using TcgStockBot.WebApi.Features.Push.Models.DTOs;
using TcgStockBot.WebApi.Features.Push.Models.Entities;
using TcgStockBot.WebApi.Features.Push.Repositories;

namespace TcgStockBot.WebApi.Features.Push.Services;

/// <summary>
/// Web Push without a third party: the VAPID keys are made on first use and kept in the data volume, every message is
/// encrypted for each device (<see cref="WebPushCrypto"/>) and posted to the browser's own push service
/// (Google, Mozilla, Apple, Microsoft). Only those push services are accepted as endpoints.
/// </summary>
/// <param name="pushRepository">Subscriptions and keys.</param>
/// <param name="options">Push settings.</param>
/// <param name="httpClientFactory">Creates the HTTP client.</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="timeProvider">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed class PushService(
    IPushRepository pushRepository,
    IOptions<WebPushOptions> options,
    IHttpClientFactory httpClientFactory,
    IGlobalizationHelper globalizationHelper,
    TimeProvider timeProvider,
    ILogger<PushService> logger) : IPushService
{
    /// <summary>The named HttpClient (its loggers are removed: endpoints carry per-device tokens).</summary>
    public const string HttpClientName = "webpush";

    /// <summary>Hosts of the browsers' push services: endpoints anywhere else are refused.</summary>
    internal static readonly string[] PushServiceHosts =
    [
        "fcm.googleapis.com", "android.googleapis.com", "push.services.mozilla.com", "push.apple.com", "notify.windows.com",
    ];

    private const string FieldEndpoint = "Endpoint";
    private const string FieldKeys = "Keys";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<PushSubscription>? _subscriptions;
    private PushKeys? _keys;

    /// <inheritdoc />
    public int DeviceCount
    {
        get
        {
            _lock.Wait();
            try
            {
                return LoadAsync().GetAwaiter().GetResult().Count;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushDTO>> GetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var keys = await KeysAsync();
            return ServiceApiResponse<PushDTO>.Success(new PushDTO
            {
                Enabled = options.Value.Enabled,
                PublicKey = keys.PublicKey,
                Devices = [.. (await LoadAsync()).OrderByDescending(static subscription => subscription.CreatedAt).Select(static subscription => new PushDeviceDTO(subscription))],
            });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PushSubscribeResponse>> SubscribeAsync(PushSubscribeRequest item)
    {
        var validationErrors = await ValidateAsync(item);
        if (validationErrors.Count > 0)
        {
            return ServiceApiResponse<PushSubscribeResponse>.ValidationProblems(
                await globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems))).AddErrorRange(validationErrors);
        }

        await _lock.WaitAsync();
        try
        {
            var subscriptions = await LoadAsync();
            var endpoint = item.Endpoint.Trim();
            var subscription = subscriptions.FirstOrDefault(existing => existing.Endpoint == endpoint);
            if (subscription is null)
            {
                subscription = new PushSubscription { Id = IdOf(endpoint), Endpoint = endpoint, CreatedAt = timeProvider.GetUtcNow() };
                subscriptions.Add(subscription);
            }

            subscription.P256dh = item.P256dh.Trim();
            subscription.Auth = item.Auth.Trim();
            subscription.UserName = Clip(item.UserName, 64);
            subscription.Device = Clip(item.Device, 60);
            subscription.LastError = string.Empty;
            await pushRepository.SaveAsync(subscriptions);

            return ServiceApiResponse<PushSubscribeResponse>.Success(new PushSubscribeResponse { Id = subscription.Id });
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> UnsubscribeAsync(PushUnsubscribeRequest item)
    {
        await _lock.WaitAsync();
        try
        {
            var subscriptions = await LoadAsync();
            var removed = subscriptions.RemoveAll(subscription =>
                (!string.IsNullOrWhiteSpace(item.Id) && subscription.Id == item.Id.Trim())
                || (!string.IsNullOrWhiteSpace(item.Endpoint) && subscription.Endpoint == item.Endpoint.Trim()));
            if (removed > 0)
            {
                await pushRepository.SaveAsync(subscriptions);
            }

            return ServiceApiResponse<string>.Success();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var subscriptions = await LoadAsync();
            if (subscriptions.Count == 0 || messages.Count == 0)
            {
                return 0;
            }

            var keys = await KeysAsync();
            var settings = options.Value;
            var client = httpClientFactory.CreateClient(HttpClientName);
            var reached = 0;
            var gone = new List<PushSubscription>();

            foreach (var subscription in subscriptions)
            {
                var error = string.Empty;
                foreach (var message in messages)
                {
                    var (status, detail) = await PostAsync(client, subscription, message, keys, settings, cancellationToken);
                    if (status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    {
                        gone.Add(subscription);   // the browser unsubscribed or the app was removed
                        error = string.Empty;
                        break;
                    }

                    if (status is not (HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted or HttpStatusCode.NoContent))
                    {
                        error = detail;
                        break;
                    }
                }

                if (gone.Contains(subscription))
                {
                    continue;
                }

                subscription.LastError = error;
                if (error.Length == 0)
                {
                    subscription.LastSentAt = timeProvider.GetUtcNow();
                    reached++;
                }
            }

            subscriptions.RemoveAll(gone.Contains);
            await pushRepository.SaveAsync(subscriptions);

            if (gone.Count > 0 && logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Push: {Count} device(s) no longer subscribed, removed", gone.Count);

            var failed = subscriptions.Where(static subscription => subscription.LastError.Length > 0).ToList();
            if (reached == 0 && failed.Count > 0)
            {
                throw new HttpRequestException($"no device could be reached ({failed[0].LastError})");
            }

            return reached;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The JSON the service worker reads (title, body, url, tag), cut to fit one encrypted record.</summary>
    internal static byte[] PayloadOf(PushMessage message)
    {
        var body = message.Body;
        while (true)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string?>
            {
                ["title"] = message.Title,
                ["body"] = body,
                ["url"] = message.Url,
                ["tag"] = message.Tag,
            });
            if (json.Length <= WebPushCrypto.MaxPayloadBytes || body.Length == 0)
            {
                return json;
            }

            body = body[..Math.Max(0, body.Length - Math.Max(50, json.Length - WebPushCrypto.MaxPayloadBytes))] + "…";
        }
    }

    /// <summary>Whether an endpoint is an https URL of a known push service.</summary>
    internal static bool IsPushServiceEndpoint(string? endpoint) =>
        Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && PushServiceHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    private async Task<(HttpStatusCode Status, string Detail)> PostAsync(HttpClient client, PushSubscription subscription, PushMessage message, PushKeys keys, WebPushOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = new Uri(subscription.Endpoint);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new ByteArrayContent(WebPushCrypto.Encrypt(PayloadOf(message), subscription.P256dh, subscription.Auth)),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("TTL", Math.Clamp(settings.TimeToLiveSeconds, 0, 2419200).ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("Urgency", "high");
            request.Headers.TryAddWithoutValidation("Topic", TopicOf(message.Tag));
            request.Headers.TryAddWithoutValidation("Authorization",
                WebPushCrypto.VapidAuthorization(endpoint, settings.Subject, keys.PublicKey, keys.PrivateKey, timeProvider.GetUtcNow().AddHours(12)));

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (response.StatusCode, string.Empty);
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.StatusCode, $"HTTP {(int)response.StatusCode} {Trim(text)}".Trim());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or ArgumentException or CryptographicException or FormatException
            && !cancellationToken.IsCancellationRequested)
        {
            return (0, exception.Message);
        }
    }

    private async Task<List<ApiValidationError>> ValidateAsync(PushSubscribeRequest item)
    {
        var validationErrors = new List<ApiValidationError>();
        if (!IsPushServiceEndpoint(item.Endpoint))
        {
            validationErrors.Add(await globalizationHelper.ValidationErrorAsync(FieldEndpoint, nameof(TranslationKeys.PushEndpointNotAllowed)));
        }

        if (!IsKey(item.P256dh, 65) || !IsKey(item.Auth, 16))
        {
            validationErrors.Add(await globalizationHelper.ValidationErrorAsync(FieldKeys, nameof(TranslationKeys.PushKeysInvalid)));
        }

        return validationErrors;
    }

    private static bool IsKey(string? text, int length)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
        {
            return false;
        }

        try
        {
            var bytes = WebPushCrypto.FromBase64Url(text);
            return bytes.Length == length && (length != 65 || bytes[0] == 0x04);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Loads the subscriptions once; callers hold the lock.</summary>
    private async Task<List<PushSubscription>> LoadAsync() => _subscriptions ??= await pushRepository.ListAsync();

    /// <summary>The VAPID keys, made and saved the first time; callers hold the lock.</summary>
    private async Task<PushKeys> KeysAsync()
    {
        if (_keys is not null)
        {
            return _keys;
        }

        var keys = await pushRepository.GetKeysAsync();
        if (keys is null || string.IsNullOrEmpty(keys.PublicKey) || string.IsNullOrEmpty(keys.PrivateKey))
        {
            var (publicKey, privateKey) = WebPushCrypto.CreateVapidKeys();
            keys = new PushKeys { PublicKey = publicKey, PrivateKey = privateKey };
            await pushRepository.SaveKeysAsync(keys);
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Push: new VAPID keys created");
        }

        return _keys = keys;
    }

    private static string IdOf(string endpoint) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)))[..12];

    /// <summary>A Topic header (at most 32 base64url characters) so a newer push of the same product replaces an undelivered one.</summary>
    private static string TopicOf(string tag) => WebPushCrypto.Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(tag)))[..32];

    private static string Clip(string? text, int length)
    {
        var value = (text ?? string.Empty).Trim();
        return value.Length <= length ? value : value[..length];
    }

    private static string Trim(string text) => text.Length <= 200 ? text : text[..200];
}
