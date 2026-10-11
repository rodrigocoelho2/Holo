namespace TcgStockBot.Website.Security;

/// <summary>
/// Sign-in settings, bound from the "Dashboard" section (in Docker: Dashboard__Password, Dashboard__AllowRegistration,
/// Dashboard__KeysDirectory).
/// </summary>
public sealed class DashboardOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Dashboard";

    /// <summary>
    /// Gets or sets the setup key (DASHBOARD_PASSWORD): needed to create the FIRST account, so nobody else on the
    /// network can claim an empty install. Empty lets anyone who reaches the page create the first account.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether anyone may create an account after the first one (false: only admins add accounts).</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Gets or sets where the cookie encryption keys are kept. Empty keeps them in memory, so every restart signs everyone out.
    /// </summary>
    public string KeysDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets how long "Keep me signed in" keeps a device signed in.</summary>
    public int RememberDays { get; set; } = 30;

    /// <summary>Gets a value indicating whether the first account needs the setup key.</summary>
    public bool RequiresSetupKey => !string.IsNullOrEmpty(Password);
}
