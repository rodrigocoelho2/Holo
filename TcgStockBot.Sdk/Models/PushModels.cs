namespace TcgStockBot.Sdk.Models;

/// <summary>Push state: on or off, the public key and the devices (mirror of the WebApi PushDTO).</summary>
public sealed class PushDTO
{
    public bool Enabled { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public List<PushDeviceDTO> Devices { get; set; } = [];
}

/// <summary>One device that receives push notifications.</summary>
public sealed class PushDeviceDTO
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSentAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}

/// <summary>Input of PushSubscribe: the browser's subscription.</summary>
public sealed class PushSubscribeRequest
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
}

/// <summary>Output of PushSubscribe.</summary>
public sealed class PushSubscribeResponse
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>Input of PushUnsubscribe.</summary>
public sealed class PushUnsubscribeRequest
{
    public string? Id { get; set; }
    public string? Endpoint { get; set; }
}
