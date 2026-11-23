using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using HB_NLP_Research_Lab.Core;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// GenerateEngineAsync used to publish a complete performance card: 2.5 MN thrust,
/// 97% efficiency, 380 s impulse, and an innovation level of 0.98. Nothing was
/// designed, so those fields stay unset. The architecture type and material names stay.
/// </summary>
public class GenerateEngineStandInTests
{
    [Fact]
    public async Task GenerateEngineAsync_DoesNotPublishAnInventedPerformanceCard()
    {
        var engine = await new AutonomousEngineDesigner().GenerateEngineAsync("demo-engine");

        engine.Id.Should().Be("demo-engine");
        engine.Architecture.ArchitectureType.Should().Be("Revolutionary AI-Generated");
        engine.Architecture.MaterialComposition.PrimaryMaterial.Should().Be("Quantum-Enhanced Superalloy");
        engine.Architecture.MaterialComposition.SecondaryMaterial.Should().Be("Graphene Composite");
        engine.Architecture.MaterialComposition.Coating.Should().Be("Thermal Barrier Coating");
        engine.Architecture.MaterialComposition.Strength.Should().Be(0);
        engine.Architecture.MaterialComposition.TemperatureResistance.Should().Be(0);

        engine.Architecture.PropulsionSystem.Thrust.Should().Be(0);
        engine.Architecture.PropulsionSystem.Efficiency.Should().Be(0);
        engine.Architecture.PropulsionSystem.SpecificImpulse.Should().Be(0);
        engine.Architecture.PropulsionSystem.ChamberPressure.Should().Be(0);
        engine.Architecture.CoolingSystem.Efficiency.Should().Be(0);
        engine.Architecture.CoolingSystem.CoolingCapacity.Should().Be(0);
        engine.Architecture.ControlSystem.ResponseTime.Should().Be(0);
        engine.Architecture.ControlSystem.Accuracy.Should().Be(0);
        engine.Architecture.InnovationLevel.Should().Be(0);

        engine.Performance.Thrust.Should().Be(0);
        engine.Performance.Efficiency.Should().Be(0);
        engine.Performance.Reliability.Should().Be(0);
        engine.Performance.FuelConsumption.Should().Be(0);
        engine.Performance.Temperature.Should().Be(0);
        engine.Performance.Pressure.Should().Be(0);
        engine.Performance.RPM.Should().Be(0);
        engine.Performance.PowerOutput.Should().Be(0);
        engine.Performance.SpecificImpulse.Should().Be(0);
        engine.Performance.ThrustToWeightRatio.Should().Be(0);
        engine.OptimizationEfficiency.Should().Be(0);
        engine.InnovationLevel.Should().Be(0);
        engine.OptimizationHistory.Should().BeEmpty();
        engine.OptimizedParameters.Should().BeEmpty();
    }
}
