using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Notifications.Models.DTOs;

/// <summary>
/// Input of NotificationTest: sends a sample alert through one channel.
/// </summary>
public sealed class NotificationTestRequest
{
    /// <summary>Gets or sets the channel to test.</summary>
    public NotificationChannel Channel { get; set; }
}
