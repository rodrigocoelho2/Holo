using Microsoft.Extensions.Options;
using TcgStockBot.WebApi.Features;

namespace TcgStockBot.WebApi.Extensions.Data;

/// <summary>
/// Owns the bot's data files. It is the only class that touches the disk; repositories go through it.
/// Reads return independent copies, writes are serialized and atomic.
/// </summary>
internal sealed class BotDataContext
{
    private const string SeedSectionName = "Bot";

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private BotConfiguration _configuration;

    /// <summary>
    /// Loads data/bot-config.json, or creates it from the "Bot" section of appsettings.json on first start.
    /// </summary>
    /// <param name="options">Module options (data folder).</param>
    /// <param name="configuration">Application configuration (seed).</param>
    /// <param name="logger">Logger.</param>
    public BotDataContext(IOptions<TcgStockBotOptions> options, IConfiguration configuration, ILogger<BotDataContext> logger)
    {
        DataDirectory = Path.GetFullPath(options.Value.DataDirectory);
        Directory.CreateDirectory(DataDirectory);

        var saved = JsonFile.Read<BotConfiguration>(ConfigurationPath);
        if (saved is not null)
        {
            _configuration = saved;
            return;
        }

        _configuration = configuration.GetSection(SeedSectionName).Get<BotConfiguration>() ?? new BotConfiguration();
        File.WriteAllText(ConfigurationPath, System.Text.Json.JsonSerializer.Serialize(_configuration, JsonFile.SerializerOptions));

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Created {ConfigurationPath} from appsettings.json", ConfigurationPath);
        }
    }

    /// <summary>Gets the absolute data folder.</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the path of the editable configuration.</summary>
    public string ConfigurationPath => Path.Combine(DataDirectory, "bot-config.json");

    /// <summary>Gets the path of the "already alerted" memory.</summary>
    public string AlertStatePath => Path.Combine(DataDirectory, "alert-state.json");

    /// <summary>Gets the path of the alert history.</summary>
    public string AlertHistoryPath => Path.Combine(DataDirectory, "alert-history.json");

    /// <summary>Gets the path of the price history.</summary>
    public string PriceHistoryPath => Path.Combine(DataDirectory, "price-history.json");

    /// <summary>Gets the path of the manual corrections of the Compare grouping.</summary>
    public string CompareOverridesPath => Path.Combine(DataDirectory, "compare-overrides.json");

    /// <summary>Gets the path of the dashboard accounts.</summary>
    public string UsersPath => Path.Combine(DataDirectory, "users.json");

    /// <summary>Gets the path of the launch calendar.</summary>
    public string ReleasesPath => Path.Combine(DataDirectory, "releases.json");

    /// <summary>Gets the optional MSRP reference prices file (when present it replaces the embedded defaults).</summary>
    public string MsrpPath => Path.Combine(DataDirectory, "msrp.json");

    /// <summary>
    /// Returns an independent copy of the configuration.
    /// </summary>
    /// <returns>A copy the caller may change freely.</returns>
    public BotConfiguration Snapshot() => JsonFile.Clone(Volatile.Read(ref _configuration));

    /// <summary>
    /// Applies a change to a copy of the configuration and persists it.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is saved.</returns>
    public async Task WriteAsync(Action<BotConfiguration> change, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var copy = Snapshot();
            change(copy);
            await JsonFile.WriteAsync(ConfigurationPath, copy, cancellationToken);
            Volatile.Write(ref _configuration, copy);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
