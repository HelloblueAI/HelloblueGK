using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Start and optimize used to publish 1.5 MN and 1.6 MN of thrust with a
/// chamber temperature and a fuel flow. Nothing was measured, so those
/// fields stay unset.
/// </summary>
public class AdvancedEngineCoreStandInTests
{
    [Fact]
    public async Task StartEngineAsync_DoesNotPublishUnmeasuredPerformance()
    {
        var performance = await new AdvancedEngineCore(new EngineConfig()).StartEngineAsync();

        AssertUnmeasured(performance);
    }

    [Fact]
    public async Task OptimizePerformanceAsync_DoesNotPublishUnmeasuredPerformance()
    {
        var performance = await new AdvancedEngineCore(new EngineConfig()).OptimizePerformanceAsync();

        AssertUnmeasured(performance);
    }

    private static void AssertUnmeasured(EnginePerformance performance)
    {
        performance.Thrust.Should().Be(0);
        performance.Temperature.Should().Be(0);
        performance.FuelConsumption.Should().Be(0);
        performance.Efficiency.Should().Be(0);
        performance.Reliability.Should().Be(0);
        performance.Pressure.Should().Be(0);
        performance.RPM.Should().Be(0);
        performance.PowerOutput.Should().Be(0);
        performance.SpecificImpulse.Should().Be(0);
        performance.ThrustToWeightRatio.Should().Be(0);
    }
}
