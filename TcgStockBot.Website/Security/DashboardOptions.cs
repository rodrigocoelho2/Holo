namespace TcgStockBot.Website.Security;

/// <summary>
/// Dashboard settings, bound from the "Dashboard" section (in Docker: Dashboard__Password, Dashboard__KeysDirectory).
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>Gets or sets the dashboard password (login form). Empty leaves the dashboard open — only acceptable on 127.0.0.1.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where the cookie encryption keys are kept. Empty keeps them in memory, so every restart signs everyone out.
    /// </summary>
    public string KeysDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets how long "manter sessão" keeps a device signed in.</summary>
    public int RememberDays { get; set; } = 30;

    /// <summary>Gets a value indicating whether a password is required.</summary>
    public bool RequiresPassword => !string.IsNullOrEmpty(Password);
}
