namespace TcgStockBot.WebApi.Features.Push.Models.Entities;

/// <summary>
/// A browser that turned push notifications on (push-subscriptions.json). The endpoint and keys come from the browser's
/// PushManager; they are not secrets of ours but are never shown in full.
/// </summary>
public sealed class PushSubscription
{
    /// <summary>Gets or sets the short identifier (from the endpoint).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the push service URL of this browser.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser's P-256 public key (base64url).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser's authentication secret (base64url).</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Gets or sets the Holo account that turned it on.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets a label for the device ("iPhone · Safari").</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was turned on (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when a push last reached the push service (UTC).</summary>
    public DateTimeOffset? LastSentAt { get; set; }

    /// <summary>Gets or sets the last delivery error (empty when the last one worked).</summary>
    public string LastError { get; set; } = string.Empty;
}

/// <summary>
/// The server's VAPID key pair (push-keys.json): made once, so the subscriptions stay valid across restarts.
/// </summary>
public sealed class PushKeys
{
    /// <summary>Gets or sets the public key (base64url, 65-byte uncompressed P-256 point).</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the private key (base64url, 32 bytes). Secret: never logged nor returned.</summary>
    public string PrivateKey { get; set; } = string.Empty;
}
