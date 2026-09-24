namespace TcgStockBot.WebApi.Features.Notifications.Models;

/// <summary>
/// CallMeBot WhatsApp settings, bound from the "WhatsApp" section (in Docker: WhatsApp__* variables).
/// The free CallMeBot API is for personal use and only sends to the number that activated it.
/// </summary>
public sealed class WhatsAppOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "WhatsApp";

    /// <summary>Gets or sets a value indicating whether alerts are sent by WhatsApp.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets your phone number with country code, e.g. +351912345678.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Gets or sets the API key CallMeBot sent you. Never in appsettings.json — use WhatsApp__ApiKey.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the CallMeBot endpoint.</summary>
    public string ApiUrl { get; set; } = "https://api.callmebot.com/whatsapp.php";

    /// <summary>Gets or sets how many alerts one message lists before summarising the rest.</summary>
    public int MaxAlertsPerMessage { get; set; } = 5;
}
