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
