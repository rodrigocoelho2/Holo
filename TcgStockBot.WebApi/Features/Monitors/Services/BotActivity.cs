using TcgStockBot.WebApi.Features.Alerts.Models;
using TcgStockBot.WebApi.Features.Monitors.Models.DTOs;

namespace TcgStockBot.WebApi.Features.Monitors.Services;

/// <summary>
/// In-memory record of what the bot is doing, read by the dashboard. Thread-safe. Not persisted:
/// after a restart the feed starts empty and the first cycle fills the searches again.
/// </summary>
/// <param name="timeProvider">Clock.</param>
internal sealed class BotActivity(TimeProvider timeProvider)
{
    private const int MaxEvents = 300;
    private const int MaxCycles = 100;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, SearchReportDTO> _searches = [];
    private readonly LinkedList<ActivityEventDTO> _events = new();
    private readonly LinkedList<CycleSummaryDTO> _cycles = new();
    private DateTimeOffset? _currentCycleStartedAt;
    private DateTimeOffset? _nextCheckAt;

    /// <summary>
    /// Marks the start of a cycle.
    /// </summary>
    public void CycleStarted()
    {
        lock (_lock)
        {
            _currentCycleStartedAt = timeProvider.GetUtcNow();
            _nextCheckAt = null;
        }

        Log("info", "Verificação iniciada");
    }

    /// <summary>
    /// Marks the end of a cycle.
    /// </summary>
    /// <param name="searches">Searches run.</param>
    /// <param name="failed">Searches that failed.</param>
    /// <param name="matches">Matching listings.</param>
    /// <param name="newAlerts">New alerts sent.</param>
    public void CycleFinished(int searches, int failed, int matches, int newAlerts)
    {
        lock (_lock)
        {
            _cycles.AddFirst(new CycleSummaryDTO
            {
                StartedAt = _currentCycleStartedAt ?? timeProvider.GetUtcNow(),
                FinishedAt = timeProvider.GetUtcNow(),
                Searches = searches,
                Failed = failed,
                Matches = matches,
                NewAlerts = newAlerts,
            });

            if (_cycles.Count > MaxCycles)
            {
                _cycles.RemoveLast();
            }

            _currentCycleStartedAt = null;
        }

        Log(failed > 0 ? "warn" : "info",
            $"Verificação concluída: {searches - failed}/{searches} pesquisas ok, {matches} em stock, {newAlerts} alerta(s) novo(s)");
    }

    /// <summary>
    /// Records the latest search of a task on a store.
    /// </summary>
    /// <param name="report">The search report.</param>
    public void SearchCompleted(SearchReportDTO report)
    {
        lock (_lock)
        {
            _searches[StockAlert.SearchKey(report.StoreName, report.StockTaskId)] = report;
        }

        if (!report.Succeeded)
        {
            Log("error", $"{report.StoreName}: falhou a pesquisa de '{report.StockTaskId}' — {report.Error}");
        }
    }

    /// <summary>
    /// Sets when the next cycle is due.
    /// </summary>
    /// <param name="nextCheckAt">The due time, or null while paused.</param>
    public void SetNextCheck(DateTimeOffset? nextCheckAt)
    {
        lock (_lock)
        {
            _nextCheckAt = nextCheckAt;
        }
    }

    /// <summary>
    /// Adds a line to the activity feed.
    /// </summary>
    /// <param name="level">info, warn, error or alert.</param>
    /// <param name="message">The message.</param>
    public void Log(string level, string message)
    {
        lock (_lock)
        {
            _events.AddFirst(new ActivityEventDTO { At = timeProvider.GetUtcNow(), Level = level, Message = message });
            if (_events.Count > MaxEvents)
            {
                _events.RemoveLast();
            }
        }
    }

    /// <summary>
    /// Returns a consistent copy of the current state.
    /// </summary>
    /// <param name="paused">The persisted pause flag.</param>
    /// <returns>The state.</returns>
    public MonitorDTO Snapshot(bool paused)
    {
        lock (_lock)
        {
            return new MonitorDTO
            {
                Paused = paused,
                IsChecking = _currentCycleStartedAt is not null,
                CurrentCycleStartedAt = _currentCycleStartedAt,
                NextCheckAt = _nextCheckAt,
                LastCycle = _cycles.First?.Value,
                RecentCycles = [.. _cycles],
                Searches = [.. _searches.Values],
                Events = [.. _events],
            };
        }
    }
}
