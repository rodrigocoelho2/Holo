namespace TcgStockBot.WebApi.Features.Monitors.Services;

/// <summary>
/// Lets the dashboard wake the worker ("check now", "resume") instead of waiting for the next interval.
/// </summary>
internal sealed class CycleScheduler
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    /// <summary>
    /// Asks for a cycle as soon as possible. Repeated requests collapse into one.
    /// </summary>
    public void RequestRun()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A run is already requested.
        }
    }

    /// <summary>
    /// Waits for <paramref name="delay"/> or a <see cref="RequestRun"/>.
    /// </summary>
    /// <param name="delay">How long to wait (InfiniteTimeSpan while paused).</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="cancellationToken">Stops waiting when the app shuts down.</param>
    /// <returns>True when woken by a request, false when the delay elapsed.</returns>
    public async Task<bool> WaitAsync(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(delay, timeProvider, linked.Token);
        var signalTask = _signal.WaitAsync(linked.Token);

        await Task.WhenAny(delayTask, signalTask);
        await linked.CancelAsync();

        try
        {
            await Task.WhenAll(delayTask, signalTask);
        }
        catch (OperationCanceledException)
        {
            // The loser of the race was cancelled.
        }

        cancellationToken.ThrowIfCancellationRequested();
        return signalTask.IsCompletedSuccessfully;
    }
}
