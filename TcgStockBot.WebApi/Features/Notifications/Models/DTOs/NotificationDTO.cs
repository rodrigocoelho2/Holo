namespace TcgStockBot.WebApi.Features.Notifications.Models.DTOs;

/// <summary>
/// State of the notification channels (no secrets).
/// </summary>
public sealed class NotificationDTO
{
    /// <summary>Gets or sets the email channel state.</summary>
    public EmailNotificationDTO Email { get; set; } = new();

    /// <summary>Gets or sets the WhatsApp channel state.</summary>
    public WhatsAppNotificationDTO WhatsApp { get; set; } = new();

    /// <summary>Gets or sets the Telegram channel.</summary>
    public TelegramNotificationDTO Telegram { get; set; } = new();
}

/// <summary>
/// State of the email channel.
/// </summary>
public sealed class EmailNotificationDTO
{
    /// <summary>Gets or sets a value indicating whether email is on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the SMTP host.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipients.</summary>
    public List<string> To { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether a password is configured.</summary>
    public bool HasPassword { get; set; }
}

/// <summary>
/// State of the WhatsApp channel.
/// </summary>
public sealed class WhatsAppNotificationDTO
{
    /// <summary>Gets or sets a value indicating whether WhatsApp is on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the phone number with all but the last 3 digits hidden.</summary>
    public string MaskedPhone { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether an API key is configured.</summary>
    public bool HasApiKey { get; set; }
}

/// <summary>
/// The Telegram channel as configured (never the bot token).
/// </summary>
public sealed class TelegramNotificationDTO
{
    /// <summary>Gets or sets a value indicating whether Telegram alerts are on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the chat id with all but the last digits hidden.</summary>
    public string MaskedChatId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a bot token is set.</summary>
    public bool HasBotToken { get; set; }
}
