using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Performance metrics used to publish a 95% optimization level. No solver
/// optimization is measured, so OptimizationLevel stays 0. 0 means unmeasured.
/// CpuUsage is a separate stand-in and is not asserted here.
/// </summary>
public class OptimizationLevelStandInTests
{
    [Fact]
    public async Task GetOptimizationLevelAsync_DoesNotPublishAFixedLevel()
    {
        var level = await new PerformanceOptimizer().GetOptimizationLevelAsync();

        level.Should().Be(0);
    }

    [Fact]
    public async Task GetPerformanceMetricsAsync_DoesNotPublishFixedOptimizationLevel()
    {
        var engine = new HighPerformancePhysicsEngine();
        var status = await engine.InitializeAsync();

        var first = await engine.GetPerformanceMetricsAsync();
        var second = await engine.GetPerformanceMetricsAsync();

        status.OptimizationLevel.Should().Be(0);
        status.PerformanceMetrics.OptimizationLevel.Should().Be(0);
        first.OptimizationLevel.Should().Be(0);
        second.OptimizationLevel.Should().Be(0);
    }

    [Fact]
    public async Task HelloblueGKEngine_GetPerformanceMetricsAsync_DoesNotPublishFixedOptimizationLevel()
    {
        using var engine = new HelloblueGKEngine();

        var metrics = await engine.GetPerformanceMetricsAsync();

        metrics.OptimizationLevel.Should().Be(0);
    }
}
