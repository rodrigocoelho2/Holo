namespace TcgStockBot.WebApi.Features.Push.Models;

/// <summary>
/// Push notifications to the phones and computers that turned them on in Holo (Web Push). Configured in .env as
/// WebPush__*; the keys are created on the first start and kept in the data volume.
/// </summary>
public sealed class WebPushOptions
{
    /// <summary>The configuration section.</summary>
    public const string SectionName = "WebPush";

    /// <summary>Gets or sets a value indicating whether alerts are pushed to the subscribed devices.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how the push services can reach whoever runs this bot: "mailto:you@example.com" or an https URL.
    /// Apple refuses pushes without a valid one.
    /// </summary>
    public string Subject { get; set; } = "mailto:holo@localhost";

    /// <summary>Gets or sets how long a push service keeps an alert for a device that is offline, in seconds.</summary>
    public int TimeToLiveSeconds { get; set; } = 3600;

    /// <summary>Gets or sets how many products get a notification of their own; the rest are summed up in one.</summary>
    public int MaxAlertsPerPush { get; set; } = 4;
}
