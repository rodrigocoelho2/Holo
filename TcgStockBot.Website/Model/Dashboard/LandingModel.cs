namespace TcgStockBot.Website.Model.Dashboard;

/// <summary>
/// Model of the public page that explains the app before signing in.
/// </summary>
public sealed class LandingModel
{
    /// <summary>Gets a value indicating whether the visitor is already signed in (the button opens the dashboard instead of the login).</summary>
    public bool SignedIn { get; init; }

    /// <summary>Gets the version shown in the footer.</summary>
    public string Version { get; init; } = DashboardModel.CurrentVersion;
}
