namespace TcgStockBot.Library.Enums;

/// <summary>
/// Where alerts can be sent.
/// </summary>
public enum NotificationChannel
{
    /// <summary>SMTP email.</summary>
    Email,

    /// <summary>WhatsApp message through CallMeBot.</summary>
    WhatsApp,

    /// <summary>Telegram (Bot API).</summary>
    Telegram,

    /// <summary>Push notifications to the devices that turned them on (Web Push).</summary>
    Push,
}
