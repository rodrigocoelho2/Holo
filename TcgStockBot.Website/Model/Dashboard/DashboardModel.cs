namespace TcgStockBot.Website.Model.Dashboard;

/// <summary>
/// Model of the dashboard page. The page loads its data through the JSON actions, so the model only carries page-level values.
/// </summary>
public sealed class DashboardModel
{
    /// <summary>Gets the application name shown in the sidebar.</summary>
    public string ApplicationName { get; init; } = "TcgStockBot";

    /// <summary>Gets the version shown in the sidebar.</summary>
    public string Version { get; init; } = "v3.0";

    /// <summary>Gets the refresh interval of the live data, in milliseconds.</summary>
    public int PollMilliseconds { get; init; } = 3000;
}
