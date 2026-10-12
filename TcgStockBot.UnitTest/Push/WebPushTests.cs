using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Push.Models;
using TcgStockBot.WebApi.Features.Push.Models.DTOs;
using TcgStockBot.WebApi.Features.Push.Repositories;
using TcgStockBot.WebApi.Features.Push.Services;
using TcgStockBot.WebApi.Features.Stores.Services.Adapters;

namespace TcgStockBot.UnitTest.Push;

/// <summary>
/// Web Push: the encryption a browser can open (RFC 8291), the VAPID token the push services check (RFC 8292), and
/// the devices kept by the bot.
/// </summary>
public sealed class WebPushTests : IDisposable
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/device-token";

    private readonly TestDataContext _data = new();

    public void Dispose() => _data.Dispose();

    [Fact]
    public void Encrypt_CanBeOpenedByTheBrowser()
    {
        var (browser, p256dh, auth) = Browser();
        var payload = Encoding.UTF8.GetBytes("{\"title\":\"🟢 In stock · €54.99\"}");

        var body = WebPushCrypto.Encrypt(payload, p256dh, auth);

        Assert.Equal(payload, Decrypt(body, browser, WebPushCrypto.FromBase64Url(auth)));
        Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
        Assert.Equal(65, body[20]);
    }

    [Fact]
    public void Encrypt_UsesAFreshKeyAndSaltEveryTime()
    {
        var (_, p256dh, auth) = Browser();
        var payload = Encoding.UTF8.GetBytes("same");

        Assert.NotEqual(WebPushCrypto.Encrypt(payload, p256dh, auth), WebPushCrypto.Encrypt(payload, p256dh, auth));
    }

    [Fact]
    public void VapidToken_IsSignedForThePushServiceOrigin()
    {
        var (publicKey, privateKey) = WebPushCrypto.CreateVapidKeys();
        var expires = DateTimeOffset.UtcNow.AddHours(12);

        var header = WebPushCrypto.VapidAuthorization(new Uri(Endpoint), "mailto:me@example.com", publicKey, privateKey, expires);

        Assert.StartsWith("vapid t=", header);
        Assert.EndsWith($", k={publicKey}", header);
        var token = header["vapid t=".Length..header.IndexOf(',', StringComparison.Ordinal)];
        var parts = token.Split('.');
        var claims = JsonDocument.Parse(WebPushCrypto.FromBase64Url(parts[1])).RootElement;
        Assert.Equal("https://fcm.googleapis.com", claims.GetProperty("aud").GetString());
        Assert.Equal("mailto:me@example.com", claims.GetProperty("sub").GetString());
        Assert.Equal(expires.ToUnixTimeSeconds(), claims.GetProperty("exp").GetInt64());

        var point = WebPushCrypto.FromBase64Url(publicKey);
        using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..65] } });
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), WebPushCrypto.FromBase64Url(parts[2]), HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/x", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/x", true)]
    [InlineData("https://web.push.apple.com/QOx", true)]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=x", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/x", false)]
    [InlineData("https://fcm.googleapis.com:8443/x", false)]
    [InlineData("https://evil.example/fcm.googleapis.com", false)]
    [InlineData("https://notfcm.googleapis.com.evil.example/x", false)]
    [InlineData("https://192.168.1.10/push", false)]
    public void OnlyThePushServices_AreAccepted(string endpoint, bool expected) =>
        Assert.Equal(expected, PushService.IsPushServiceEndpoint(endpoint));

    [Fact]
    public async Task Subscribe_RefusesOtherHostsAndBadKeys()
    {
        var service = Service(HttpStatusCode.Created);

        var response = await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = "https://nas.local/push", P256dh = "abc", Auth = "x" });

        Assert.False(response.IsSuccess);
        Assert.Equal(["Endpoint", "Keys"], response.Problems!.Errors.Select(static error => error.Field));
    }

    [Fact]
    public async Task Subscribe_TheSameEndpointTwice_KeepsOneDevice()
    {
        var service = Service(HttpStatusCode.Created);
        var (_, p256dh, auth) = Browser();

        var first = await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = Endpoint, P256dh = p256dh, Auth = auth, UserName = "rodas", Device = "iPhone · Holo app" });
        var second = await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = Endpoint, P256dh = p256dh, Auth = auth, UserName = "rodas", Device = "iPhone · Safari" });

        Assert.Equal(first.Result!.Id, second.Result!.Id);
        var state = (await service.GetAsync()).Result!;
        var device = Assert.Single(state.Devices);
        Assert.Equal("iPhone · Safari", device.Device);
        Assert.Equal("fcm.googleapis.com", device.Service);
        Assert.Equal(65, WebPushCrypto.FromBase64Url(state.PublicKey).Length);
        Assert.Equal(1, service.DeviceCount);
    }

    [Fact]
    public async Task Keys_AreMadeOnce_AndKept()
    {
        var first = (await Service(HttpStatusCode.Created).GetAsync()).Result!.PublicKey;
        var second = (await Service(HttpStatusCode.Created).GetAsync()).Result!.PublicKey;

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Send_PostsAnEncryptedMessage_WithVapid()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var service = Service(handler);
        var (browser, p256dh, auth) = Browser();
        await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = Endpoint, P256dh = p256dh, Auth = auth });

        var reached = await service.SendAsync([new PushMessage("🟢 In stock · €54.99", "Pitch Black ETB", "https://shop.example/p", "alert-1")], CancellationToken.None);

        Assert.Equal(1, reached);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(Endpoint, request.Uri.AbsoluteUri);
        Assert.Equal("aes128gcm", request.ContentEncoding);
        Assert.StartsWith("vapid t=", request.Authorization);
        Assert.Equal("3600", request.Ttl);
        var payload = JsonDocument.Parse(Decrypt(request.Body, browser, WebPushCrypto.FromBase64Url(auth))).RootElement;
        Assert.Equal("Pitch Black ETB", payload.GetProperty("body").GetString());
        Assert.Equal("https://shop.example/p", payload.GetProperty("url").GetString());
        Assert.NotNull(Assert.Single((await service.GetAsync()).Result!.Devices).LastSentAt);
    }

    [Fact]
    public async Task Send_ToADeviceThatIsGone_RemovesIt()
    {
        var service = Service(HttpStatusCode.Gone);
        var (_, p256dh, auth) = Browser();
        await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = Endpoint, P256dh = p256dh, Auth = auth });

        var reached = await service.SendAsync([new PushMessage("t", "b", null, "x")], CancellationToken.None);

        Assert.Equal(0, reached);
        Assert.Empty((await service.GetAsync()).Result!.Devices);
    }

    [Fact]
    public async Task Send_WhenEveryDeviceFails_Throws_AndKeepsTheError()
    {
        var service = Service(HttpStatusCode.Forbidden);
        var (_, p256dh, auth) = Browser();
        await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = Endpoint, P256dh = p256dh, Auth = auth });

        await Assert.ThrowsAsync<HttpRequestException>(() => service.SendAsync([new PushMessage("t", "b", null, "x")], CancellationToken.None));
        Assert.StartsWith("HTTP 403", Assert.Single((await service.GetAsync()).Result!.Devices).LastError);
    }

    [Fact]
    public async Task Unsubscribe_RemovesTheDevice()
    {
        var service = Service(HttpStatusCode.Created);
        var (_, p256dh, auth) = Browser();
        var id = (await service.SubscribeAsync(new PushSubscribeRequest { Endpoint = Endpoint, P256dh = p256dh, Auth = auth })).Result!.Id;

        await service.UnsubscribeAsync(new PushUnsubscribeRequest { Id = id });

        Assert.Equal(0, service.DeviceCount);
    }

    [Fact]
    public void Payload_IsCutToFitOneRecord()
    {
        var payload = PushService.PayloadOf(new PushMessage("Title", new string('x', 10_000), null, "t"));

        Assert.True(payload.Length <= WebPushCrypto.MaxPayloadBytes);
        Assert.EndsWith("…", JsonDocument.Parse(payload).RootElement.GetProperty("body").GetString());
    }

    [Fact]
    public void Alerts_BecomeOneNotificationEach_AndOneForTheRest()
    {
        var alerts = Enumerable.Range(1, 6).Select(static index => new StockAlert("etb", "ETB",
            new StoreListing($"Shop {index}", $"Pitch Black Elite Trainer Box {index}", new Uri($"https://shop{index}.example/p"), 54.99m, true),
            LanguageToConfirm: false, Shipping: 0m)).ToList();

        var messages = WebPushNotifier.Messages(alerts, 4);

        Assert.Equal(5, messages.Count);
        Assert.Equal("🟢 In stock · €54.99", messages[0].Title);
        Assert.Contains("Shop 1 · €54.99 · free shipping", messages[0].Body);
        Assert.Equal("https://shop1.example/p", messages[0].Url);
        Assert.Equal("+2 more in Holo", messages[4].Title);
        Assert.Null(messages[4].Url);
    }

    private PushService Service(HttpStatusCode status) => Service(new RecordingHandler(status));

    private PushService Service(RecordingHandler handler) => new(
        new PushRepository(_data.Context),
        Options.Create(new WebPushOptions()),
        new FakeHttpClientFactory(handler),
        new GlobalizationHelper(),
        TimeProvider.System,
        NullLogger<PushService>.Instance);

    /// <summary>A browser's push keys: its P-256 key pair and the 16-byte auth secret.</summary>
    private static (ECDiffieHellman Key, string P256dh, string Auth) Browser()
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        return (key, WebPushCrypto.Base64Url([0x04, .. q.X!, .. q.Y!]), WebPushCrypto.Base64Url(RandomNumberGenerator.GetBytes(16)));
    }

    /// <summary>What the browser does with a push body (RFC 8291 from the receiving side).</summary>
    private static byte[] Decrypt(byte[] body, ECDiffieHellman browser, byte[] authSecret)
    {
        var salt = body[..16];
        var idLength = body[20];
        var serverPublic = body[21..(21 + idLength)];
        var ciphertext = body[(21 + idLength)..^16];
        var tag = body[^16..];

        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] },
        });
        var q = browser.ExportParameters(false).Q;
        byte[] browserPublic = [0x04, .. q.X!, .. q.Y!];
        var shared = browser.DeriveRawSecretAgreement(server.PublicKey);
        byte[] info = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. browserPublic, .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, info);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        Assert.Equal(0x02, plaintext[^1]);
        return plaintext[..^1];
    }

    /// <summary>Answers every request with one status and keeps what was sent.</summary>
    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(Uri Uri, byte[] Body, string ContentEncoding, string Authorization, string Ttl)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                string.Join(",", request.Content.Headers.ContentEncoding),
                request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.First() : string.Empty,
                request.Headers.TryGetValues("TTL", out var ttl) ? ttl.First() : string.Empty));
            return new HttpResponseMessage(status) { Content = new StringContent(status == HttpStatusCode.Forbidden ? "bad jwt" : string.Empty) };
        }
    }
}
