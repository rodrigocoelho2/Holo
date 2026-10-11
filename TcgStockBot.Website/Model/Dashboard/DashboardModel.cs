namespace TcgStockBot.Website.Model.Dashboard;

/// <summary>
/// Model of the dashboard page. The page loads its data through the JSON actions, so the model only carries page-level values.
/// </summary>
public sealed class DashboardModel
{
    /// <summary>The product name.</summary>
    public const string ProductName = "Card Forge";

    /// <summary>The version shown in the app.</summary>
    public const string CurrentVersion = "v4.0";

    /// <summary>Gets the application name shown in the sidebar.</summary>
    public string ApplicationName { get; init; } = ProductName;

    /// <summary>Gets the version shown in the sidebar.</summary>
    public string Version { get; init; } = CurrentVersion;

    /// <summary>Gets the refresh interval of the live data, in milliseconds.</summary>
    public int PollMilliseconds { get; init; } = 3000;

    /// <summary>Gets a value indicating whether the dashboard has a password (shows "Terminar sessão").</summary>
    public bool CanSignOut { get; init; }
}
