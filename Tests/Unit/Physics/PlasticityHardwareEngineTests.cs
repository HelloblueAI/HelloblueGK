using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.Physics;
using Xunit;

namespace HelloblueGK.Tests.Unit.Physics;

/// <summary>
/// The hardware facade used to name an RTX 4090 and return fixed utilization, temperature,
/// and power. This process does not query a device, so those figures stay absent.
/// </summary>
public class PlasticityHardwareEngineTests
{
    [Fact]
    public async Task HardwareMetrics_DoNotReportAnAccelerator()
    {
        var engine = new PlasticityHardwareEngine();
        await engine.InitializeAsync();

        var metrics = await engine.GetHardwareMetricsAsync();

        metrics.IsHardwareAvailable.Should().BeFalse();
        double.IsFinite(metrics.GpuUtilization).Should().BeFalse();
        double.IsFinite(metrics.HardwareUtilization).Should().BeFalse();
        double.IsFinite(metrics.MemoryUsage).Should().BeFalse();
        double.IsFinite(metrics.CpuUtilization).Should().BeFalse();
        double.IsFinite(metrics.Temperature).Should().BeFalse();
        double.IsFinite(metrics.PowerConsumption).Should().BeFalse();
        metrics.PerformanceMetrics.ComputationSpeed.Should().Be(0);
        metrics.PerformanceMetrics.Efficiency.Should().Be(0);
    }
}
