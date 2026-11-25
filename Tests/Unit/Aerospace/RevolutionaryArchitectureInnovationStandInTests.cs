using System;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.Aerospace;
using Xunit;

namespace HelloblueGK.Tests.Unit.Aerospace;

/// <summary>
/// Architecture factories used to stamp innovation levels of 0.95, 0.90, 0.92,
/// 0.88, and 0.96. Nothing was measured, and a request spec is not a result, so
/// the level and the average that only restates it stay unset.
/// </summary>
public class RevolutionaryArchitectureInnovationStandInTests
{
    [Fact]
    public async Task ArchitectureFactories_DoNotPublishAnUnmeasuredInnovationLevel()
    {
        var architectures = new RevolutionaryEngineArchitectures();

        var variable = await architectures.CreateVariableGeometryEngineAsync(
            "vg-1",
            new VariableGeometrySpecs
            {
                GeometryStates = 99,
                MinExpansionRatio = 1,
                MaxExpansionRatio = 99,
                MorphingResponseTime = 9.9
            });
        var modular = await architectures.CreateModularEngineSystemAsync(
            "mod-1",
            new ModularSystemSpecs
            {
                ModuleCount = 99,
                StandardizedComponents = true,
                TargetAssemblyTime = TimeSpan.FromHours(9),
                AutomationLevel = 0.01
            });
        var distributed = await architectures.CreateDistributedPropulsionSystemAsync(
            "dist-1",
            new DistributedPropulsionSpecs
            {
                EngineCount = 99,
                TotalThrust = 1,
                CoordinationAccuracy = 0.01,
                RedundancyRequired = true
            });
        var hybrid = await architectures.CreateHybridElectricEngineAsync(
            "hyb-1",
            new HybridElectricSpecs
            {
                ElectricPower = 1,
                CombustionThrust = 1,
                BatteryCapacity = 1,
                RegenerativeCapability = true
            });
        var nuclear = await architectures.CreateNuclearThermalEngineAsync(
            "nuc-1",
            new NuclearThermalSpecs
            {
                ReactorPower = 1,
                FuelType = "not-a-measurement",
                SafetyFactor = 99,
                EmergencySystems = true
            });

        variable.ArchitectureType.Should().Be("Variable Geometry");
        modular.ArchitectureType.Should().Be("Modular Design");
        distributed.ArchitectureType.Should().Be("Distributed Propulsion");
        hybrid.ArchitectureType.Should().Be("Hybrid Electric");
        nuclear.ArchitectureType.Should().Be("Nuclear Thermal");
        new[] { variable, modular, distributed, hybrid, nuclear }
            .Should().OnlyContain(engine => engine.InnovationLevel == 0);

        var summary = await architectures.GenerateArchitectureSummaryAsync();
        summary.TotalEngines.Should().Be(5);
        summary.AverageInnovationLevel.Should().Be(0);
        summary.ArchitectureTypes.Should().BeEquivalentTo(
            "Variable Geometry",
            "Modular Design",
            "Distributed Propulsion",
            "Hybrid Electric",
            "Nuclear Thermal");
    }

    [Fact]
    public async Task ArchitectureSummary_WithNoEngines_ReportsAnUnmeasuredAverage()
    {
        var summary = await new RevolutionaryEngineArchitectures().GenerateArchitectureSummaryAsync();

        summary.TotalEngines.Should().Be(0);
        summary.AverageInnovationLevel.Should().Be(0);
        summary.ArchitectureTypes.Should().BeEmpty();
    }
}
