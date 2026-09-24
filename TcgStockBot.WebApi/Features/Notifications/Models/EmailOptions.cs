namespace TcgStockBot.WebApi.Features.Notifications.Models;

/// <summary>
/// SMTP settings, bound from the "Email" section (in Docker: Email__* variables).
/// </summary>
public sealed class EmailOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Email";

    /// <summary>Gets or sets a value indicating whether alerts are emailed.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the SMTP host.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP port.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Gets or sets a value indicating whether to use STARTTLS.</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Gets or sets the SMTP user name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP password. Never in appsettings.json — use user-secrets or Email__Password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender address.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipients.</summary>
    public List<string> To { get; set; } = [];
}
