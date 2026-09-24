namespace TcgStockBot.WebApi.Features.Stores.Services.Adapters.Awin;

/// <summary>
/// Awin affiliate product feeds, bound from the "AwinFeeds" section (in Docker: AwinFeeds__Feeds__ElCorteIngles, …).
/// A feed is only available after the advertiser approves you in its Awin programme; the download URL is the one
/// Awin's Create-a-Feed tool generates. It contains your feed API key, so it is a secret: .env / user-secrets only.
/// </summary>
public sealed class AwinFeedOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "AwinFeeds";

    /// <summary>The shortest refresh interval allowed (Awin asks publishers not to download feeds more often than they change).</summary>
    public static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the feed download URLs by provider key ("Fnac", "Worten", "ElCorteIngles").
    /// Empty or missing = that store has no authorized feed configured.
    /// </summary>
    public Dictionary<string, string> Feeds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets how long a downloaded feed is reused before downloading it again (minimum 1 hour).</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>Gets or sets how long to wait after a failed download before trying that feed again.</summary>
    public TimeSpan RetryAfterFailure { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the words a product title must contain (any of them, accents ignored) to be kept in memory.
    /// Large retailers' feeds have hundreds of thousands of products; only these are kept. Empty = keep everything.
    /// </summary>
    public List<string> KeepKeywords { get; set; } = ["pokemon"];

    /// <summary>Gets or sets the maximum number of products kept per feed.</summary>
    public int MaxProducts { get; set; } = 5000;

    /// <summary>
    /// The effective refresh interval.
    /// </summary>
    /// <returns>The configured interval, never below <see cref="MinimumRefreshInterval"/>.</returns>
    public TimeSpan EffectiveRefreshInterval() =>
        RefreshInterval < MinimumRefreshInterval ? MinimumRefreshInterval : RefreshInterval;
}
