using TcgStockBot.WebApi.Features.Monitors.Services;

namespace TcgStockBot.UnitTest.Monitors;

public class CycleSchedulerTests
{
    [Fact]
    public async Task WaitAsync_ReturnsFalse_WhenTheDelayElapses() =>
        Assert.False(await new CycleScheduler().WaitAsync(TimeSpan.FromMilliseconds(20), TimeProvider.System, CancellationToken.None));

    [Fact]
    public async Task WaitAsync_ReturnsTrueImmediately_WhenARunIsRequested()
    {
        var scheduler = new CycleScheduler();
        var waiting = scheduler.WaitAsync(Timeout.InfiniteTimeSpan, TimeProvider.System, CancellationToken.None);

        scheduler.RequestRun();

        Assert.True(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RequestRun_BeforeWaiting_IsNotLost_AndRepeatedRequestsCollapse()
    {
        var scheduler = new CycleScheduler();
        scheduler.RequestRun();
        scheduler.RequestRun();

        Assert.True(await scheduler.WaitAsync(Timeout.InfiniteTimeSpan, TimeProvider.System, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(await scheduler.WaitAsync(TimeSpan.FromMilliseconds(20), TimeProvider.System, CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_Throws_WhenStopping()
    {
        var scheduler = new CycleScheduler();
        using var stopping = new CancellationTokenSource();
        var waiting = scheduler.WaitAsync(Timeout.InfiniteTimeSpan, TimeProvider.System, stopping.Token);

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}
