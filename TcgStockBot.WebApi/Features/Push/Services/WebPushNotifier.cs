using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TcgStockBot.Library.Enums;
using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Notifications.Services;
using TcgStockBot.WebApi.Features.Push.Models;

namespace TcgStockBot.WebApi.Features.Push.Services;

/// <summary>
/// Alerts as push notifications on the devices that turned them on: one notification per product (a tap opens the
/// store page), the rest summed up in one more.
/// </summary>
/// <param name="pushService">The devices and the sending.</param>
/// <param name="options">Push settings.</param>
/// <param name="logger">Logger.</param>
internal sealed class WebPushNotifier(IPushService pushService, IOptions<WebPushOptions> options, ILogger<WebPushNotifier> logger) : INotifier
{
    /// <inheritdoc />
    public NotificationChannel? Channel => NotificationChannel.Push;

    /// <inheritdoc />
    public bool IsEnabled => options.Value.Enabled && pushService.DeviceCount > 0;

    /// <inheritdoc />
    public async Task SendAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
    {
        var reached = await pushService.SendAsync(Messages(alerts, options.Value.MaxAlertsPerPush), cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Alert push sent ({Count} product(s), {Devices} device(s))", alerts.Count, reached);
    }

    /// <inheritdoc />
    public async Task SendNoticeAsync(string subject, string text, CancellationToken cancellationToken)
    {
        var body = text.Length <= 400 ? text : text[..400] + "…";
        await pushService.SendAsync([new PushMessage(subject, body, null, "notice-" + Hash(subject))], cancellationToken);
    }

    /// <summary>One notification per product, up to <paramref name="maxAlerts"/>, and one for the rest.</summary>
    internal static List<PushMessage> Messages(IReadOnlyList<StockAlert> alerts, int maxAlerts)
    {
        var shown = Math.Max(1, maxAlerts);
        var messages = alerts.Take(shown).Select(static alert =>
        {
            var (title, body) = AlertMessageFormatter.Push(alert);
            return new PushMessage(title, body, alert.Listing.Url.AbsoluteUri, "alert-" + Hash(alert.Key));
        }).ToList();

        if (alerts.Count > shown)
        {
            var rest = alerts.Skip(shown).ToList();
            messages.Add(new PushMessage($"+{rest.Count} more in Holo",
                string.Join('\n', rest.Take(5).Select(static alert => $"{alert.Listing.Title} · {alert.Listing.StoreName}")), null, "alert-more"));
        }

        return messages;
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
}
