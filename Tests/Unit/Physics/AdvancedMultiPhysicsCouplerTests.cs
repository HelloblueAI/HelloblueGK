using FluentAssertions;
using HB_NLP_Research_Lab.Physics;

namespace HelloblueGK.Tests.Unit.Physics;

public class AdvancedMultiPhysicsCouplerTests
{
    [Fact]
    public async Task RunCoupledAnalysisAsync_ConcurrentCalls_ReturnIndependentHistories()
    {
        var coupler = new AdvancedMultiPhysicsCoupler();
        await coupler.InitializeAsync();

        var engineA = new EngineModel
        {
            Name = "CouplerEngineA",
            Parameters = new Dictionary<string, object>
            {
                ["Thrust"] = 1_000_000d,
                ["ChamberPressure"] = 28_000_000d
            }
        };
        var engineB = new EngineModel
        {
            Name = "CouplerEngineB",
            Parameters = new Dictionary<string, object>
            {
                ["Thrust"] = 1_200_000d,
                ["ChamberPressure"] = 30_000_000d
            }
        };

        var results = await Task.WhenAll(
            coupler.RunCoupledAnalysisAsync(engineA),
            coupler.RunCoupledAnalysisAsync(engineB));

        results.Should().HaveCount(2);
        results.Should().OnlyContain(result => result.CouplingHistory != null);
        results[0].CouplingHistory.Should().NotBeSameAs(results[1].CouplingHistory);
        // The monitor reports a residual equal to the tolerance. That is converged,
        // so the loop stops on the first pass instead of compounding for 50 iterations.
        results.Should().OnlyContain(result => result.TotalIterations == 1);

        var baseline = (AdvancedThermalResult)new AdvancedThermalSolver().RunSimulation(new EngineModel
        {
            Name = "CouplerBaseline",
            Parameters = new Dictionary<string, object>
            {
                ["ChamberPressure"] = 28_000_000d,
                ["ChamberTemperature"] = 3500d
            }
        });
        results.Should().OnlyContain(result =>
            result.ThermalAnalysis.HeatTransferCoefficients["Convection"]
            == baseline.HeatTransferCoefficients["Convection"]);
    }

    [Fact]
    public void ResidualHasConverged_AcceptsAResidualAtTheTolerance()
    {
        AdvancedMultiPhysicsCoupler.ResidualHasConverged(1e-6, 1e-6).Should().BeTrue();
        AdvancedMultiPhysicsCoupler.ResidualHasConverged(0, 1e-6).Should().BeTrue();
        AdvancedMultiPhysicsCoupler.ResidualHasConverged(1e-6 + 1e-12, 1e-6).Should().BeFalse();
        AdvancedMultiPhysicsCoupler.ResidualHasConverged(-1e-9, 1e-6).Should().BeFalse();
        AdvancedMultiPhysicsCoupler.ResidualHasConverged(double.NaN, 1e-6).Should().BeFalse();
        AdvancedMultiPhysicsCoupler.ResidualHasConverged(1e-6, double.NaN).Should().BeFalse();
    }

    [Fact]
    public void ScaleHeatTransferByDeformation_UsesPeakDisplacementNotGridSize()
    {
        var untouched = new double[100, 100];
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(150, untouched).Should().Be(150);
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(150, null).Should().Be(150);
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(150, new double[0, 0]).Should().Be(150);

        var displaced = new double[100, 1];
        displaced[0, 0] = -10;
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(150, displaced)
            .Should().Be(150 * 1.1);

        var withGap = new double[1, 2];
        withGap[0, 0] = double.NaN;
        withGap[0, 1] = 4;
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(100, withGap)
            .Should().Be(104);

        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(double.NaN, untouched).Should().Be(0);
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(double.PositiveInfinity, null).Should().Be(0);
        AdvancedMultiPhysicsCoupler.ScaleHeatTransferByDeformation(double.MaxValue, displaced)
            .Should().Be(double.MaxValue);
    }

    [Fact]
    public void ApplyConvectionCoupling_LeavesAMissingCoefficientAbsent()
    {
        var coefficients = new Dictionary<string, double>();

        AdvancedMultiPhysicsCoupler.ApplyConvectionCoupling(coefficients, new double[100, 100]);

        coefficients.Should().BeEmpty();
    }

    [Fact]
    public async Task RunMultiPhysicsAnalysisAsync_RejectsAnEngineIdWithNoOperatingPoint()
    {
        var coupler = new AdvancedMultiPhysicsCoupler();

        var act = () => coupler.RunMultiPhysicsAnalysisAsync("HB-NLP-REV-001");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("engineId");
    }
}
