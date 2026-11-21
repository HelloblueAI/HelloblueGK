using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Generated architectures used to publish thrust, impulse, chamber pressure,
/// cooling, strength, and temperature resistance. Nothing was measured, and a
/// request target is not a result, so those fields stay unset.
/// </summary>
public class PropulsionCoolingMaterialStandInTests
{
    [Fact]
    public async Task RevolutionaryDesign_DoesNotPublishInventedPropulsionCoolingOrMaterials()
    {
        var requirements = new DesignRequirements
        {
            RequiredThrust = 9_999_999,
            RequiredEfficiency = 0.42,
            MaxTemperature = 1234
        };

        var result = await new AutonomousEngineDesigner()
            .GenerateRevolutionaryEngineAsync(requirements);

        result.AllArchitectures.Should().NotBeEmpty();
        result.AllArchitectures.Should().OnlyContain(architecture => IsUnmeasured(architecture));
        result.OptimizedEngines.Should().NotBeEmpty();
        result.OptimizedEngines.Should().OnlyContain(engine =>
            IsUnmeasured(engine.Architecture)
            && engine.Performance.Thrust == 0
            && engine.Performance.Efficiency == 0);
        IsUnmeasured(result.BestEngine.Architecture).Should().BeTrue();
        result.BestEngine.Performance.Thrust.Should().Be(0);
        result.BestEngine.Performance.Efficiency.Should().Be(0);
    }

    private static bool IsUnmeasured(EngineArchitecture architecture)
    {
        var propulsion = architecture.PropulsionSystem;
        var cooling = architecture.CoolingSystem;
        var material = architecture.MaterialComposition;
        return propulsion.Type == "Advanced Methane/LOX"
            && propulsion.Thrust == 0
            && propulsion.SpecificImpulse == 0
            && propulsion.ChamberPressure == 0
            && propulsion.Efficiency == 0
            && cooling.Type == "Active Regenerative Cooling"
            && cooling.CoolingCapacity == 0
            && cooling.Efficiency == 0
            && material.PrimaryMaterial == "Advanced Nickel Superalloy"
            && material.SecondaryMaterial == "Carbon-Carbon Composite"
            && material.Coating == "Thermal Barrier Coating"
            && material.Strength == 0
            && material.TemperatureResistance == 0;
    }
}
