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
        // Mean kinetic energy over the grid, not the running sum. The sum is larger by
        // the sample count and used to report an intensity of about 471.
        cfd.TurbulenceIntensity.Should().BeApproximately(0.47105095265799735, 1e-12);
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
        high.TurbulenceIntensity.Should().Be(low.TurbulenceIntensity);
    }

    [Fact]
    public void AdvancedCFDSolver_PressureFieldUsesTheOperatingPointSpecificHeatRatio()
    {
        var solver = new AdvancedCFDSolver();
        var products = (AdvancedCFDResult)solver.RunSimulation(OperatingPoint(20e6, specificHeatRatio: 1.2));
        var air = (AdvancedCFDResult)solver.RunSimulation(OperatingPoint(20e6, specificHeatRatio: 1.4));

        products.PressureDistribution[0, 0].Should().Be(air.PressureDistribution[0, 0]);
        products.PressureDistribution[500, 500].Should().BeGreaterThan(air.PressureDistribution[500, 500]);
        products.TurbulenceIntensity.Should().Be(air.TurbulenceIntensity);

        var model = new EngineModel { Name = "no-gamma" };
        model.Parameters["ChamberPressure"] = 20e6;
        var schematicAir = (AdvancedCFDResult)solver.RunSimulation(model);

        schematicAir.PressureDistribution[500, 500].Should().Be(air.PressureDistribution[500, 500]);

        var missingRatio = (AdvancedCFDResult)solver.RunSimulation(OperatingPoint(20e6, specificHeatRatio: double.NaN));
        var unityRatio = (AdvancedCFDResult)solver.RunSimulation(OperatingPoint(20e6, specificHeatRatio: 1.0));
        missingRatio.PressureDistribution[500, 500].Should().Be(air.PressureDistribution[500, 500]);
        unityRatio.PressureDistribution[500, 500].Should().Be(air.PressureDistribution[500, 500]);
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

        // Peak displacement is the chamber-wall value of the field, in metres.
        // stress/E (5e-5 m at 20 MPa) is the axial strain and is not that peak.
        low.MaxDisplacement.Should().Be(low.DisplacementField[0, 0]);
        low.MaxDisplacement.Should().BeApproximately(1.3e-4, 1e-12);
        low.MaxDisplacement.Should().BeGreaterThan(10e6 / 200e9);
        low.DisplacementField[999, 999].Should().BeLessThan(low.MaxDisplacement);
        high.MaxDisplacement.Should().Be(high.DisplacementField[0, 0]);
        (high.MaxDisplacement / low.MaxDisplacement).Should().BeApproximately(2.0, 1e-9);

        (high.DisplacementField[0, 0] / low.DisplacementField[0, 0]).Should().BeApproximately(2.0, 1e-9);
        ((double)high.BucklingAnalysis["AppliedPressure"]).Should().Be(40e6);
        ((double)high.FatigueAnalysis["AppliedStress"]).Should().Be(20e6);

        // The map is the analysis. It is not the old constants 2.5 and 1.8.
        low.SafetyFactors["BucklingSafetyFactor"].Should().Be(low.BucklingAnalysis["BucklingSafetyFactor"]);
        low.SafetyFactors["FatigueSafetyFactor"].Should().Be(low.FatigueAnalysis["SafetyFactor"]);
        ((double)high.SafetyFactors["BucklingSafetyFactor"])
            .Should().BeLessThan((double)low.SafetyFactors["BucklingSafetyFactor"]);
        ((double)high.SafetyFactors["FatigueSafetyFactor"])
            .Should().BeLessThan((double)low.SafetyFactors["FatigueSafetyFactor"]);
        ((double)low.SafetyFactors["BucklingSafetyFactor"]).Should().NotBe(2.5);
        ((double)low.SafetyFactors["FatigueSafetyFactor"]).Should().NotBe(1.8);
        low.FailurePrediction["CyclesToFailure"].Should().Be(low.FatigueAnalysis["CyclesToFailure"]);

        low.FailurePrediction["FailureMode"].Should().Be("Safe");

        // Above the shell's critical pressure, still below steel yield
        // (reported stress is half the chamber pressure; yield is 250 MPa).
        var buckled = (AdvancedStructuralResult)solver.RunSimulation(OperatingPoint(200e6));
        buckled.FailurePrediction["YieldFailure"].Should().Be(false);
        buckled.FailurePrediction["BucklingFailure"].Should().Be(true);
        buckled.FailurePrediction["FailureMode"].Should().Be("Buckling");
        ((double)buckled.SafetyFactors["BucklingSafetyFactor"]).Should().BeLessThan(1);
        buckled.SafetyFactors["BucklingSafetyFactor"].Should().Be(buckled.BucklingAnalysis["BucklingSafetyFactor"]);

        var yielded = (AdvancedStructuralResult)solver.RunSimulation(OperatingPoint(600e6));
        yielded.FailurePrediction["FailureMode"].Should().Be("Yield");
        yielded.FailurePrediction["FatigueFailure"].Should().Be(false);

        var shortLife = (AdvancedStructuralResult)solver.RunSimulation(OperatingPoint(2e9));
        shortLife.FailurePrediction["FailureMode"].Should().Be("Yield");
        shortLife.FailurePrediction["FatigueFailure"].Should().Be(true);
        ((double)shortLife.FailurePrediction["CyclesToFailure"]).Should().BeLessThan(1e5);
        shortLife.FailurePrediction["CyclesToFailure"].Should().Be(shortLife.FatigueAnalysis["CyclesToFailure"]);
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
        double chamberTemperatureKelvin = 3600,
        double specificHeatRatio = 1.2) => new()
    {
        ChamberPressure = chamberPressurePascals,
        ChamberTemperature = chamberTemperatureKelvin,
        SpecificHeatRatio = specificHeatRatio,
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
