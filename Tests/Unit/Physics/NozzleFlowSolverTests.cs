using System;
using FluentAssertions;
using HB_NLP_Research_Lab.Physics;
using Xunit;

namespace HelloblueGK.Tests.Unit.Physics;

/// <summary>
/// Contract tests for the one solver in this project whose result depends on its argument.
///
/// These exist as a counterweight to <c>PhysicsSolverContractTests</c>. The schematic CFD and
/// structural solvers scale a closed-form field by chamber pressure and refuse a model that has
/// none; this solver derives thrust and specific impulse from the whole operating point. The
/// difference is visible in the test suite rather than something a reader has to discover by
/// reading the implementations.
/// </summary>
public class NozzleFlowSolverTests
{
    private static EngineOperatingPoint OperatingPoint(double throatArea = 0.05, double ambientPressure = 101325) =>
        new()
        {
            ChamberPressure = 9.7e6,
            ChamberTemperature = 3500,
            SpecificHeatRatio = 1.24,
            MolarMass = 0.0223,
            ExpansionRatio = 16,
            ThroatArea = throatArea,
            AmbientPressure = ambientPressure
        };

    [Fact]
    public void RunSimulation_ProducesASolutionDerivedFromTheOperatingPoint()
    {
        var solver = new NozzleFlowSolver();

        var result = solver.RunSimulation(OperatingPoint());

        result.Status.Should().Be("Success");
        result.Should().BeOfType<NozzleFlowResult>();

        var solution = ((NozzleFlowResult)result).Solution;
        solution.Thrust.Should().BePositive();
        solution.SpecificImpulse.Should().BeInRange(250, 300);
        solution.MassFlowRate.Should().BePositive();

        // Data mirrors the headline figures for callers that only read the base PhysicsResult.
        result.Data.Should().HaveCount(4);
        result.Data[0].Should().Be(solution.Thrust);
        result.Data[1].Should().Be(solution.SpecificImpulse);
    }

    /// <summary>
    /// Two different engines must not produce the same answer. This is the exact failure the
    /// other solvers exhibit, so it is worth an explicit test rather than leaving it implied.
    /// </summary>
    [Fact]
    public void RunSimulation_DifferentOperatingPointsProduceDifferentResults()
    {
        var solver = new NozzleFlowSolver();

        var small = (NozzleFlowResult)solver.RunSimulation(OperatingPoint(throatArea: 0.02));
        var large = (NozzleFlowResult)solver.RunSimulation(OperatingPoint(throatArea: 0.20));

        large.Solution.Thrust.Should().BeGreaterThan(small.Solution.Thrust);
        large.Solution.MassFlowRate.Should().BeGreaterThan(small.Solution.MassFlowRate);
    }

    /// <summary>
    /// Passing the wrong kind of model must fail loudly. Falling back to a default operating point
    /// would reproduce the defect this solver was written to avoid: a result that looks valid while
    /// having nothing to do with the engine the caller asked about.
    /// </summary>
    [Fact]
    public void RunSimulation_RejectsAModelItCannotInterpret()
    {
        var solver = new NozzleFlowSolver();

        var thrown = Assert.Throws<ArgumentException>(() => solver.RunSimulation(new object()));

        thrown.Message.Should().Contain(nameof(EngineOperatingPoint));
    }

    [Fact]
    public void RunSimulation_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new NozzleFlowSolver().RunSimulation(null!));
    }

    /// <summary>
    /// The solver is stateless, so Initialize must be optional and repeated calls must agree. The
    /// CFD solver needs an initialisation step because it allocates fields; this one does not, and
    /// callers should be able to rely on that.
    /// </summary>
    [Fact]
    public void RunSimulation_IsIndependentOfInitializationAndRepeatable()
    {
        var withoutInit = (NozzleFlowResult)new NozzleFlowSolver().RunSimulation(OperatingPoint());

        var initialized = new NozzleFlowSolver();
        initialized.Initialize();
        var withInit = (NozzleFlowResult)initialized.RunSimulation(OperatingPoint());
        var secondRun = (NozzleFlowResult)initialized.RunSimulation(OperatingPoint());

        withInit.Solution.Thrust.Should().Be(withoutInit.Solution.Thrust);
        secondRun.Solution.Thrust.Should().Be(withInit.Solution.Thrust);
    }

    [Fact]
    public void Name_IdentifiesTheModelBeingSolved()
    {
        new NozzleFlowSolver().Name.Should().Contain("Ideal Rocket Nozzle");
    }
}
