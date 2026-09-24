namespace TcgStockBot.WebApi.Features;

/// <summary>
/// Module options, bound from the "TcgStockBot" configuration section.
/// </summary>
public sealed class TcgStockBotOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "TcgStockBot";

    /// <summary>Gets or sets the folder holding bot-config.json, alert-state.json and alert-history.json. Relative paths are resolved against the content root.</summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>Gets or sets the key the Website must send in the x-api-key header. Never commit a real value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTTP timeout for store requests.</summary>
    public TimeSpan StoreRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
