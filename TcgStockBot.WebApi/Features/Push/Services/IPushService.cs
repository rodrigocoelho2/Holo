using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Features.Push.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Push.Services;

/// <summary>
/// One notification as the service worker shows it.
/// </summary>
/// <param name="Title">The bold first line.</param>
/// <param name="Body">The text under it.</param>
/// <param name="Url">What a tap opens (a store page, or a Holo path); null = Holo.</param>
/// <param name="Tag">Same tag = the newer notification replaces the older one on the device.</param>
internal sealed record PushMessage(string Title, string Body, string? Url, string Tag);

/// <summary>
/// The devices that turned push on, and sending to them.
/// </summary>
internal interface IPushService
{
    /// <summary>Gets the number of subscribed devices.</summary>
    int DeviceCount { get; }

    /// <summary>
    /// Gets the state: on or off, the public key and the devices.
    /// </summary>
    /// <returns>The state.</returns>
    Task<ServiceApiResponse<PushDTO>> GetAsync();

    /// <summary>
    /// Adds a device, or updates it when its endpoint is already known.
    /// </summary>
    /// <param name="item">The browser's subscription.</param>
    /// <returns>The device identifier, or validation problems.</returns>
    Task<ServiceApiResponse<PushSubscribeResponse>> SubscribeAsync(PushSubscribeRequest item);

    /// <summary>
    /// Removes a device (nothing happens when it is not known).
    /// </summary>
    /// <param name="item">The device, by identifier or endpoint.</param>
    /// <returns>Success.</returns>
    Task<ServiceApiResponse<string>> UnsubscribeAsync(PushUnsubscribeRequest item);

    /// <summary>
    /// Sends notifications to every device. Devices the push service says are gone are removed.
    /// </summary>
    /// <param name="messages">The notifications.</param>
    /// <param name="cancellationToken">Cancels the sending.</param>
    /// <returns>How many devices got them; throws when every device failed.</returns>
    Task<int> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken);
}
