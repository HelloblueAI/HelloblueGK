using System.Threading;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Control;

namespace HelloblueGK.Tests.Unit.Core;

public class RealTimeControlLoopTests
{
    [Fact]
    public async Task Start_AfterStop_RunsTheLoopAgain()
    {
        using var loop = new CountingLoop(100);

        await loop.StartAsync();
        (await WaitUntilAsync(() => loop.Executions > 0, TimeSpan.FromSeconds(2))).Should().BeTrue();

        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
        var stoppedAt = loop.Executions;

        await loop.StartAsync();
        loop.IsRunning.Should().BeTrue();
        (await WaitUntilAsync(() => loop.Executions > stoppedAt, TimeSpan.FromSeconds(2))).Should().BeTrue();

        var alreadyRunning = async () => await loop.StartAsync();
        await alreadyRunning.Should().ThrowAsync<InvalidOperationException>();
        loop.IsRunning.Should().BeTrue();

        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
        var stoppedAgain = loop.Executions;

        await loop.StartAsync();
        (await WaitUntilAsync(() => loop.Executions > stoppedAgain, TimeSpan.FromSeconds(2))).Should().BeTrue();
        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task LatePass_DoesNotBurstTheMissedPeriods()
    {
        // 250 ms at 100 Hz is 25 periods. Catch-up used to run those periods
        // before the first wait, which is one second of throttle rate in a burst.
        using var loop = new StallingLoop(100, TimeSpan.FromMilliseconds(250));

        await loop.StartAsync();
        (await WaitUntilAsync(() => loop.ExecutionsAtFirstWait > 0, TimeSpan.FromSeconds(2))).Should().BeTrue();

        loop.ExecutionsAtFirstWait.Should().BeInRange(2, 4);
        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task OnTimePass_WaitsBetweenPasses()
    {
        using var loop = new CountingLoop(50);

        await loop.StartAsync();
        (await WaitUntilAsync(() => loop.Executions >= 3, TimeSpan.FromSeconds(2))).Should().BeTrue();

        // 50 Hz with no wait would already be far past this by the first poll.
        loop.Executions.Should().BeLessThan(12);
        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Start_ImmediatelyAfterStop_RunsTheLoopAgain()
    {
        using var loop = new CountingLoop(100);

        await loop.StartAsync();
        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
        var stoppedAt = loop.Executions;

        await loop.StartAsync();
        (await WaitUntilAsync(() => loop.Executions > stoppedAt, TimeSpan.FromSeconds(2))).Should().BeTrue();
        loop.IsRunning.Should().BeTrue();
        await loop.StopAsync();
        loop.IsRunning.Should().BeFalse();
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return true;

            await Task.Delay(10);
        }

        return predicate();
    }

    private sealed class CountingLoop : RealTimeControlLoop
    {
        private int _executions;

        public CountingLoop(int frequencyHz) : base(frequencyHz)
        {
        }

        public int Executions => Volatile.Read(ref _executions);

        protected override Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executions);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The first pass blocks. Later passes return immediately and record how many
    /// had already run when the loop first asked to wait.
    /// </summary>
    private sealed class StallingLoop : RealTimeControlLoop
    {
        private readonly TimeSpan _stall;
        private int _executions;
        private int _executionsAtFirstWait;

        public StallingLoop(int frequencyHz, TimeSpan stall) : base(frequencyHz)
        {
            _stall = stall;
        }

        public int ExecutionsAtFirstWait => Volatile.Read(ref _executionsAtFirstWait);

        protected override async Task ExecuteControlLoopAsync(CancellationToken cancellationToken)
        {
            var execution = Interlocked.Increment(ref _executions);
            if (execution == 1)
                await Task.Delay(_stall, cancellationToken).ConfigureAwait(false);
        }

        protected override Task WaitForNextPassAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
                Interlocked.CompareExchange(ref _executionsAtFirstWait, Volatile.Read(ref _executions), 0);

            return base.WaitForNextPassAsync(delay, cancellationToken);
        }
    }
}
