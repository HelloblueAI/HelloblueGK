using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Performance metrics used to publish a random 0–100 CPU percent. No host
/// sample is attached, so CpuUsage stays 0. 0 means unmeasured.
/// OptimizationLevel is a separate stand-in and is not asserted here.
/// </summary>
public class CpuUsageStandInTests
{
    [Fact]
    public async Task GetPerformanceMetricsAsync_DoesNotPublishRandomCpuUsage()
    {
        var engine = new HighPerformancePhysicsEngine();
        var status = await engine.InitializeAsync();

        var first = await engine.GetPerformanceMetricsAsync();
        var second = await engine.GetPerformanceMetricsAsync();

        status.PerformanceMetrics.CpuUsage.Should().Be(0);
        first.CpuUsage.Should().Be(0);
        second.CpuUsage.Should().Be(0);
    }

    [Fact]
    public async Task HelloblueGKEngine_GetPerformanceMetricsAsync_DoesNotPublishRandomCpuUsage()
    {
        using var engine = new HelloblueGKEngine();

        var metrics = await engine.GetPerformanceMetricsAsync();

        metrics.CpuUsage.Should().Be(0);
    }
}
