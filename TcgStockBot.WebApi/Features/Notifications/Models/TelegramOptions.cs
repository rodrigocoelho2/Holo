namespace TcgStockBot.WebApi.Features.Notifications.Models;

/// <summary>
/// Telegram alerts through a bot of your own (created with @BotFather). Configured in .env as Telegram__*.
/// </summary>
public sealed class TelegramOptions
{
    /// <summary>The configuration section.</summary>
    public const string SectionName = "Telegram";

    /// <summary>Gets or sets a value indicating whether alerts are sent by Telegram.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the bot token BotFather gave you (secret: never logged nor shown).</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the chat that receives the alerts (your user id, or a group/channel id).</summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Bot API base URL.</summary>
    public string ApiUrl { get; set; } = "https://api.telegram.org";

    /// <summary>Gets or sets how many products go in one message; the rest are mentioned as "+N".</summary>
    public int MaxAlertsPerMessage { get; set; } = 8;
}
