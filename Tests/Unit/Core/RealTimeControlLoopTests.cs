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
}
