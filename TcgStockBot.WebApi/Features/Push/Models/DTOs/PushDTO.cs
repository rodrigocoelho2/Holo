using TcgStockBot.WebApi.Features.Push.Models.Entities;

namespace TcgStockBot.WebApi.Features.Push.Models.DTOs;

/// <summary>
/// Push state for the dashboard: whether it is on, the public key browsers subscribe with and the devices.
/// </summary>
public sealed class PushDTO
{
    /// <summary>Gets or sets a value indicating whether push is on (WebPush__Enabled).</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the VAPID public key (base64url) for PushManager.subscribe.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the subscribed devices.</summary>
    public List<PushDeviceDTO> Devices { get; set; } = [];
}

/// <summary>
/// One subscribed device, without its keys.
/// </summary>
public sealed class PushDeviceDTO
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PushDeviceDTO"/> class (for serialization).
    /// </summary>
    public PushDeviceDTO()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PushDeviceDTO"/> class from a subscription.
    /// </summary>
    /// <param name="entity">The subscription.</param>
    public PushDeviceDTO(PushSubscription entity)
    {
        Id = entity.Id;
        UserName = entity.UserName;
        Device = entity.Device;
        Service = Uri.TryCreate(entity.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
        CreatedAt = entity.CreatedAt;
        LastSentAt = entity.LastSentAt;
        LastError = entity.LastError;
    }

    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the account that turned it on.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the device label.</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Gets or sets the push service host (fcm.googleapis.com, web.push.apple.com…).</summary>
    public string Service { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was turned on.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when a push last went out to it.</summary>
    public DateTimeOffset? LastSentAt { get; set; }

    /// <summary>Gets or sets the last delivery error.</summary>
    public string LastError { get; set; } = string.Empty;
}

/// <summary>
/// Input of PushSubscribe: the browser's PushSubscription.
/// </summary>
public sealed class PushSubscribeRequest
{
    /// <summary>Gets or sets the push service URL.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser's public key (keys.p256dh).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser's secret (keys.auth).</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Gets or sets the Holo account (set by the Website, not the browser).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a device label.</summary>
    public string Device { get; set; } = string.Empty;
}

/// <summary>
/// Input of PushUnsubscribe: the device to remove, by identifier or endpoint.
/// </summary>
public sealed class PushUnsubscribeRequest
{
    /// <summary>Gets or sets the device identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the push service URL (what the browser knows).</summary>
    public string? Endpoint { get; set; }
}

/// <summary>
/// Output of PushSubscribe.
/// </summary>
public sealed class PushSubscribeResponse
{
    /// <summary>Gets or sets the device identifier.</summary>
    public string Id { get; set; } = string.Empty;
}
