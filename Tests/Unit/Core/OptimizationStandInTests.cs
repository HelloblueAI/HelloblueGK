using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// The architecture optimizer used to publish 95% efficiency and a larger thrust
/// and cooling efficiency. Nothing was optimized, so those fields stay unset.
/// </summary>
public class OptimizationStandInTests
{
    [Fact]
    public async Task OptimizeAsync_DoesNotScaleThrustOrCoolingEfficiency()
    {
        var architecture = new EngineArchitecture
        {
            Name = "measured-input",
            PropulsionSystem = new PropulsionSystem { Thrust = 1_500_000 },
            CoolingSystem = new CoolingSystem { Efficiency = 0.90 }
        };

        var result = await new OptimizationAlgorithm().OptimizeAsync(architecture);

        result.Efficiency.Should().Be(0);
        result.Performance.Thrust.Should().Be(0);
        result.Performance.Efficiency.Should().Be(0);
        result.Performance.Reliability.Should().Be(0);
        result.History.Should().BeEmpty();
        result.Parameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 0.5)]
    [InlineData(double.NegativeInfinity, 0.5)]
    [InlineData(-1_500_000d, 1.2)]
    [InlineData(0d, 0d)]
    public async Task OptimizeAsync_DoesNotAmplifyAnUnusableBaseline(double thrust, double coolingEfficiency)
    {
        var architecture = new EngineArchitecture
        {
            PropulsionSystem = new PropulsionSystem { Thrust = thrust },
            CoolingSystem = new CoolingSystem { Efficiency = coolingEfficiency }
        };

        var result = await new OptimizationAlgorithm().OptimizeAsync(architecture);

        result.Efficiency.Should().Be(0);
        result.Performance.Thrust.Should().Be(0);
        result.Performance.Efficiency.Should().Be(0);
        double.IsFinite(result.Performance.Thrust).Should().BeTrue();
        double.IsFinite(result.Performance.Efficiency).Should().BeTrue();
    }

    [Fact]
    public async Task RevolutionaryDesign_DoesNotPublishOptimizationEfficiency()
    {
        var result = await new AutonomousEngineDesigner()
            .GenerateRevolutionaryEngineAsync(new DesignRequirements());

        result.OptimizedEngines.Should().NotBeEmpty();
        result.OptimizationEfficiency.Should().Be(0);
        result.OptimizedEngines.Should().OnlyContain(engine =>
            engine.OptimizationEfficiency == 0
            && engine.Performance.Thrust == 0
            && engine.Performance.Efficiency == 0);
        result.BestEngine.OptimizationEfficiency.Should().Be(0);
        result.BestEngine.Performance.Thrust.Should().Be(0);
        result.BestEngine.Performance.Efficiency.Should().Be(0);
    }
}
