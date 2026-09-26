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

    private static EngineOperatingPoint OperatingPoint(
        double chamberPressurePascals,
        double chamberTemperatureKelvin = 3600) => new()
    {
        ChamberPressure = chamberPressurePascals,
        ChamberTemperature = chamberTemperatureKelvin,
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
    public void AdvancedThermalSolver_RunSimulationSelfInitializesWhenInitializeWasSkipped()
    {
        var solver = new AdvancedThermalSolver();
        var point = OperatingPoint(20e6, 2500);

        var result = (AdvancedThermalResult)solver.RunSimulation(point);

        result.Status.Should().Be("Success");
        result.Data[0].Should().Be(2500);
        result.TemperatureDistribution.GetLength(0).Should().Be(1000);
        result.TemperatureDistribution[0, 0].Should().Be(2500);
        result.HeatFluxField[0, 0].Should().BeGreaterThan(0);
        result.HeatTransferCoefficients.Should().ContainKey("Convection");
        result.ConvergenceHistory.Should().NotBeEmpty();

        var second = (AdvancedThermalResult)solver.RunSimulation(point);
        second.Data[0].Should().Be(result.Data[0]);
        second.HeatFluxField[0, 0].Should().Be(result.HeatFluxField[0, 0]);
    }

    [Fact]
    public void AdvancedThermalSolver_TemperatureFieldTracksTheSuppliedChamberTemperature()
    {
        var solver = new AdvancedThermalSolver();

        var low = (AdvancedThermalResult)solver.RunSimulation(OperatingPoint(10e6, 1800));
        var high = (AdvancedThermalResult)solver.RunSimulation(OperatingPoint(10e6, 3600));

        low.TemperatureDistribution[0, 0].Should().Be(1800);
        high.TemperatureDistribution[0, 0].Should().Be(3600);
        high.Data[0].Should().BeGreaterThan(low.Data[0]);
        (high.HeatFluxField[0, 0] / low.HeatFluxField[0, 0])
            .Should().BeApproximately((3600 - 300.0) / (1800 - 300.0), 1e-9);
        high.HeatTransferCoefficients["Radiation"]
            .Should().BeGreaterThan(low.HeatTransferCoefficients["Radiation"]);
        high.HeatTransferCoefficients["Convection"]
            .Should().Be(low.HeatTransferCoefficients["Convection"]);
        high.HeatTransferEfficiency.Should().BeGreaterThan(low.HeatTransferEfficiency);
        high.CoolingSystemPerformance["TemperatureDrop"].Should().Be(3300);
    }

    [Fact]
    public void AdvancedThermalSolver_ReadsChamberTemperatureFromAnEngineModel()
    {
        var solver = new AdvancedThermalSolver();
        var model = new EngineModel
        {
            Name = "HotWall",
            Parameters = new Dictionary<string, object> { ["ChamberTemperature"] = 2800d }
        };

        var result = (AdvancedThermalResult)solver.RunSimulation(model);

        result.TemperatureDistribution[0, 0].Should().Be(2800);
    }

    [Fact]
    public void AdvancedThermalSolver_ColdChamberReportsNoThermalEfficiency()
    {
        var solver = new AdvancedThermalSolver();

        var result = (AdvancedThermalResult)solver.RunSimulation(OperatingPoint(10e6, 200));

        result.TemperatureDistribution[0, 0].Should().Be(200);
        result.HeatTransferEfficiency.Should().Be(0);
        result.HeatFluxField[0, 0].Should().BeLessThan(0);
    }

    [Fact]
    public void AdvancedThermalSolver_RejectsAModelWithNoChamberTemperature()
    {
        var solver = new AdvancedThermalSolver();

        solver.Invoking(s => s.RunSimulation(new object()))
            .Should().Throw<ArgumentException>();
        solver.Invoking(s => s.RunSimulation(null!))
            .Should().Throw<ArgumentNullException>();
        solver.Invoking(s => s.RunSimulation(new EngineModel()))
            .Should().Throw<ArgumentException>();
        solver.Invoking(s => s.RunSimulation(OperatingPoint(10e6, 0)))
            .Should().Throw<ArgumentOutOfRangeException>();
        solver.Invoking(s => s.RunSimulation(OperatingPoint(10e6, double.NaN)))
            .Should().Throw<ArgumentOutOfRangeException>();
        solver.Invoking(s => s.RunSimulation(new EngineModel
        {
            Name = "NotANumber",
            Parameters = new Dictionary<string, object> { ["ChamberTemperature"] = double.NaN }
        })).Should().Throw<ArgumentException>();
        solver.Invoking(s => s.RunSimulation(new EngineModel
        {
            Name = "WrongType",
            Parameters = new Dictionary<string, object> { ["ChamberTemperature"] = "hot" }
        })).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AdvancedStructuralSolver_ExposesDescriptiveNameWithoutHeavyInit()
    {
        var solver = new AdvancedStructuralSolver();

        solver.Should().BeAssignableTo<IPhysicsSolver>();
        solver.Name.Should().Contain("Structural");
    }
}
