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

        var result = solver.RunSimulation(new object());

        result.Should().BeOfType<AdvancedCFDResult>();
        result.Status.Should().Be("Success");

        var cfd = (AdvancedCFDResult)result;
        cfd.PressureDistribution.GetLength(0).Should().Be(1000);
        cfd.VelocityField.GetLength(0).Should().Be(1000);
        cfd.TurbulenceIntensity.Should().BeGreaterThan(0);
        cfd.ConvergenceHistory.Should().NotBeEmpty();

        // A second run must not re-initialize or change the outcome.
        solver.RunSimulation(new object()).Status.Should().Be("Success");
    }

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
