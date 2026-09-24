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
}
