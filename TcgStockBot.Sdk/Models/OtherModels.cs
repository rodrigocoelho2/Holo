using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>Read model of a sent alert (mirror of the WebApi AlertDTO).</summary>
public sealed class AlertDTO
{
    public DateTimeOffset At { get; set; }
    public string StockTaskId { get; set; } = string.Empty;
    public string StockTaskName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool LanguageToConfirm { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? PreviousPrice { get; set; }
    public decimal? Msrp { get; set; }
    public bool PreOrder { get; set; }
}

/// <summary>Monitoring settings, durations in seconds (mirror of the WebApi SettingDTO).</summary>
public sealed class SettingDTO
{
    public bool Paused { get; set; }
    public int PollIntervalSeconds { get; set; }
    public int PollJitterSeconds { get; set; }
    public int DelayBetweenRequestsSeconds { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public int AutoDisableAfterFailures { get; set; }
    public bool DailySummaryEnabled { get; set; }
    public int DailySummaryHour { get; set; }
}

/// <summary>Input of SettingSave.</summary>
public sealed class SettingSaveRequest
{
    public int PollIntervalSeconds { get; set; }
    public int PollJitterSeconds { get; set; }
    public int DelayBetweenRequestsSeconds { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public int AutoDisableAfterFailures { get; set; } = 6;
    public bool DailySummaryEnabled { get; set; }
    public int DailySummaryHour { get; set; } = 9;
}

/// <summary>State of the notification channels (mirror of the WebApi NotificationDTO).</summary>
public sealed class NotificationDTO
{
    public EmailNotificationDTO Email { get; set; } = new();
    public WhatsAppNotificationDTO WhatsApp { get; set; } = new();
    public TelegramNotificationDTO Telegram { get; set; } = new();
    public PushNotificationDTO Push { get; set; } = new();
}

/// <summary>State of the push channel.</summary>
public sealed class PushNotificationDTO
{
    public bool Enabled { get; set; }
}

/// <summary>State of the email channel.</summary>
public sealed class EmailNotificationDTO
{
    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = [];
    public bool HasPassword { get; set; }
}

/// <summary>State of the WhatsApp channel.</summary>
public sealed class WhatsAppNotificationDTO
{
    public bool Enabled { get; set; }
    public string MaskedPhone { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
}

/// <summary>Input of NotificationTest.</summary>
public sealed class TelegramNotificationDTO
{
    public bool Enabled { get; set; }
    public string MaskedChatId { get; set; } = string.Empty;
    public bool HasBotToken { get; set; }
}

public sealed class NotificationTestRequest
{
    public NotificationChannel Channel { get; set; }
}
