using FluentAssertions;
using HB_NLP_Research_Lab.Physics;
using Xunit;

namespace HelloblueGK.Tests.Unit.Physics;

public class PhysicsSolverContractTests
{
    [Fact]
    public void PhysicsResult_DefaultsAreSafe()
    {
        var result = new PhysicsResult();

        result.Status.Should().BeEmpty();
        result.Data.Should().NotBeNull().And.BeEmpty();
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void AdvancedCFDSolver_ExposesDescriptiveNameWithoutHeavyInit()
    {
        var solver = new AdvancedCFDSolver();

        solver.Should().BeAssignableTo<IPhysicsSolver>();
        solver.Name.Should().Contain("CFD");
    }

    /// <summary>
    /// Every production caller calls Initialize() before RunSimulation(), so the
    /// self-initialising path was never exercised. It is the only branch in the solver
    /// that a caller can reach without meaning to, and the certification boundary
    /// requires full decision coverage of the file.
    /// </summary>
    [Fact]
    public void AdvancedCFDSolver_RunSimulationSelfInitializesWhenInitializeWasSkipped()
    {
        var solver = new AdvancedCFDSolver();
        var point = OperatingPoint(20e6);

        var result = solver.RunSimulation(point);

        result.Should().BeOfType<AdvancedCFDResult>();
        result.Status.Should().Be("Success");

        var cfd = (AdvancedCFDResult)result;
        cfd.PressureDistribution.GetLength(0).Should().Be(1000);
        cfd.PressureDistribution[0, 0].Should().Be(20e6);
        cfd.VelocityField.GetLength(0).Should().Be(1000);
        cfd.TurbulenceIntensity.Should().BeGreaterThan(0);
        cfd.ConvergenceHistory.Should().NotBeEmpty();

        // A second run must not re-initialize or change the outcome.
        var second = (AdvancedCFDResult)solver.RunSimulation(point);
        second.Status.Should().Be("Success");
        second.PressureDistribution[0, 0].Should().Be(cfd.PressureDistribution[0, 0]);
    }

    [Fact]
    public void AdvancedCFDSolver_PressureFieldTracksTheSuppliedChamberPressure()
    {
        var solver = new AdvancedCFDSolver();

        var low = (AdvancedCFDResult)solver.RunSimulation(OperatingPoint(10e6));
        var high = (AdvancedCFDResult)solver.RunSimulation(OperatingPoint(30e6));

        high.PressureDistribution[0, 0].Should().BeGreaterThan(low.PressureDistribution[0, 0]);
        (high.PressureDistribution[500, 500] / low.PressureDistribution[500, 500])
            .Should().BeApproximately(3.0, 1e-9);
    }

    [Fact]
    public void AdvancedCFDSolver_RejectsAModelWithNoChamberPressure()
    {
        var solver = new AdvancedCFDSolver();

        solver.Invoking(s => s.RunSimulation(new object()))
            .Should().Throw<ArgumentException>();
        solver.Invoking(s => s.RunSimulation(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AdvancedStructuralSolver_StressTracksTheSuppliedChamberPressure()
    {
        var solver = new AdvancedStructuralSolver();

        var low = (AdvancedStructuralResult)solver.RunSimulation(OperatingPoint(20e6));
        var high = (AdvancedStructuralResult)solver.RunSimulation(OperatingPoint(40e6));

        low.MaxVonMisesStress.Should().Be(10e6);
        high.MaxVonMisesStress.Should().Be(20e6);
        high.MaxDisplacement.Should().BeGreaterThan(low.MaxDisplacement);

        low.FailurePrediction["FailureMode"].Should().Be("Safe");
        var yielded = (AdvancedStructuralResult)solver.RunSimulation(OperatingPoint(600e6));
        yielded.FailurePrediction["FailureMode"].Should().Be("Yield");
    }

    [Fact]
    public void AdvancedStructuralSolver_RejectsAModelWithNoChamberPressure()
    {
        var solver = new AdvancedStructuralSolver();

        solver.Invoking(s => s.RunSimulation(new EngineModel()))
            .Should().Throw<ArgumentException>();
    }

    private static EngineOperatingPoint OperatingPoint(double chamberPressurePascals) => new()
    {
        ChamberPressure = chamberPressurePascals,
        ChamberTemperature = 3600,
        SpecificHeatRatio = 1.2,
        MolarMass = 0.0206,
        ThroatArea = 0.01,
        ExpansionRatio = 28.5,
        AmbientPressure = 0
    };

    [Fact]
    public void AdvancedThermalSolver_ExposesDescriptiveNameWithoutHeavyInit()
    {
        var solver = new AdvancedThermalSolver();

        solver.Should().BeAssignableTo<IPhysicsSolver>();
        solver.Name.Should().Contain("Thermal");
    }

    [Fact]
    public void AdvancedStructuralSolver_ExposesDescriptiveNameWithoutHeavyInit()
    {
        var solver = new AdvancedStructuralSolver();

        solver.Should().BeAssignableTo<IPhysicsSolver>();
        solver.Name.Should().Contain("Structural");
    }
}
