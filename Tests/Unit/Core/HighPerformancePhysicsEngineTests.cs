using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class HighPerformancePhysicsEngineTests
{
    [Fact]
    public async Task RunMultiPhysicsAnalysisAsync_UsesPerCallTimersUnderParallelism()
    {
        var engine = new HighPerformancePhysicsEngine();
        await engine.InitializeAsync();

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => engine.RunMultiPhysicsAnalysisAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(8);
        results.Should().OnlyContain(result =>
            result.TotalCalculationCount > 0 &&
            result.ExecutionTime >= TimeSpan.Zero &&
            double.IsFinite(result.CalculationsPerSecond));
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotentUnderConcurrency()
    {
        var engine = new HighPerformancePhysicsEngine();

        var statuses = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => engine.InitializeAsync()));

        statuses.Should().OnlyContain(status => status.IsInitialized);
        var metrics = await engine.GetPerformanceMetricsAsync();
        metrics.Uptime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }
}
