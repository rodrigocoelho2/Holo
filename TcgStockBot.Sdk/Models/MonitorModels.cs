using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>What the bot is doing right now (mirror of the WebApi MonitorDTO).</summary>
public sealed class MonitorDTO
{
    public bool Paused { get; set; }
    public bool IsChecking { get; set; }
    public DateTimeOffset? CurrentCycleStartedAt { get; set; }
    public DateTimeOffset? NextCheckAt { get; set; }
    public CycleSummaryDTO? LastCycle { get; set; }
    public List<CycleSummaryDTO> RecentCycles { get; set; } = [];
    public List<SearchReportDTO> Searches { get; set; } = [];
    public List<ActivityEventDTO> Events { get; set; } = [];
}

/// <summary>The outcome of one check cycle.</summary>
public sealed class CycleSummaryDTO
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public int Searches { get; set; }
    public int Failed { get; set; }
    public int Matches { get; set; }
    public int NewAlerts { get; set; }
}

/// <summary>The latest search of one task on one store.</summary>
public sealed class SearchReportDTO
{
    public string StoreName { get; set; } = string.Empty;
    public string StockTaskId { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public long DurationMs { get; set; }
    public List<ListingReportDTO> Listings { get; set; } = [];
    public int MatchCount { get; set; }
}

/// <summary>One listing of a search, with its verdict.</summary>
public sealed class ListingReportDTO
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public bool InStock { get; set; }
    public ProductLanguage Language { get; set; }
    public MatchOutcome Outcome { get; set; }
    public string? ProductId { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Seller { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>One line of the activity feed.</summary>
public sealed class ActivityEventDTO
{
    public DateTimeOffset At { get; set; }
    public string Level { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
}
