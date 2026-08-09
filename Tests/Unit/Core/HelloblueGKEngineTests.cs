using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Physics;

namespace HelloblueGK.Tests.Unit.Core;

public class HelloblueGKEngineTests : IDisposable
{
    private readonly HelloblueGKEngine _engine;

    public HelloblueGKEngineTests()
    {
        _engine = new HelloblueGKEngine();
    }


    [Fact]
    public async Task AnalyzeEngineAsync_ShouldReturnComprehensiveAnalysis()
    {
        // Arrange
        var engineModel = "HB-NLP-REV-001";

        // Act
        var result = await _engine.AnalyzeEngineAsync(engineModel);

        // Assert
        result.Should().NotBeNull();
        result.ThrustAnalysis.Should().NotBeNull();
        result.ThermalAnalysis.Should().NotBeNull();
        result.StructuralAnalysis.Should().NotBeNull();
        result.PerformanceMetrics.Should().NotBeEmpty();
        result.MultiPhysicsResult.Should().NotBeNull();
        result.ValidationReport.Should().NotBeNull();
        result.OptimizationResult.Should().NotBeNull();
        result.InnovationReport.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeEngineAsync_ShouldHaveValidThrustAnalysis()
    {
        // Arrange
        var engineModel = "TestEngine";

        // Act
        var result = await _engine.AnalyzeEngineAsync(engineModel);

        // Assert
        result.ThrustAnalysis.MaxThrust.Should().BeGreaterThan(0);
        result.ThrustAnalysis.Efficiency.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_ShouldHaveValidThermalAnalysis()
    {
        // Arrange
        var engineModel = "TestEngine";

        // Act
        var result = await _engine.AnalyzeEngineAsync(engineModel);

        // Assert
        result.ThermalAnalysis.MaxTemperature.Should().BeGreaterThan(0);
        result.ThermalAnalysis.CoolingEfficiency.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_ShouldHaveValidStructuralAnalysis()
    {
        // Arrange
        var engineModel = "TestEngine";

        // Act
        var result = await _engine.AnalyzeEngineAsync(engineModel);

        // Assert
        result.StructuralAnalysis.MaxStress.Should().BeGreaterThan(0);
        result.StructuralAnalysis.SafetyFactor.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task GenerateValidationSummaryAsync_ShouldReturnValidSummary()
    {
        // Act
        var summary = await _engine.GenerateValidationSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.ValidationScore.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
        summary.CriticalIssues.Should().BeGreaterThanOrEqualTo(0);
        summary.Warnings.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetPerformanceMetricsAsync_ShouldReturnValidMetrics()
    {
        // Act
        var metrics = await _engine.GetPerformanceMetricsAsync();

        // Assert
        metrics.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeEngineAsync_ShouldHandleMultipleCalls()
    {
        // Arrange
        var engineModel = "TestEngine";

        // Act
        var result1 = await _engine.AnalyzeEngineAsync(engineModel);
        var result2 = await _engine.AnalyzeEngineAsync(engineModel);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithCfdType_UsesCfdSolverPath()
    {
        var result = await _engine.AnalyzeEngineAsync(
            "TestEngine",
            "CFD",
            new Dictionary<string, object> { ["iterations"] = 42 });

        result.SimulationType.Should().Be("CFD");
        // Client iterations overrides are ignored — ConvergenceIterations stay solver-owned.
        result.Iterations.Should().Be(150);
        result.ThrustAnalysis.MaxThrust.Should().BeGreaterThan(0);
        result.ThermalAnalysis.MaxTemperature.Should().Be(0);
        result.StructuralAnalysis.MaxStress.Should().Be(0);
        result.MultiPhysicsResult.TotalCalculationCount.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithThermalType_UsesThermalSolverPath()
    {
        var result = await _engine.AnalyzeEngineAsync("TestEngine", "Thermal");

        result.SimulationType.Should().Be("Thermal");
        result.Iterations.Should().Be(120);
        result.ThermalAnalysis.MaxTemperature.Should().BeGreaterThan(0);
        result.ThrustAnalysis.MaxThrust.Should().Be(0);
        result.StructuralAnalysis.MaxStress.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithStructuralType_UsesStructuralSolverPath()
    {
        var result = await _engine.AnalyzeEngineAsync("TestEngine", "Structural");

        result.SimulationType.Should().Be("Structural");
        result.Iterations.Should().Be(100);
        result.StructuralAnalysis.MaxStress.Should().BeGreaterThan(0);
        result.ThrustAnalysis.MaxThrust.Should().Be(0);
        result.ThermalAnalysis.MaxTemperature.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithCfdParameters_AppliesThrustAndPressureToResults()
    {
        var result = await _engine.AnalyzeEngineAsync(
            "TestEngine",
            "CFD",
            new Dictionary<string, object>
            {
                ["chamberPressure"] = 275.5,
                ["thrust"] = 1234567,
                ["efficiency"] = 0.91,
                ["iterations"] = 88
            });

        result.SimulationType.Should().Be("CFD");
        result.Iterations.Should().Be(150);
        result.ThrustAnalysis.MaxThrust.Should().Be(1234567);
        result.ThrustAnalysis.Efficiency.Should().BeApproximately(0.91, 0.0001);
        result.PerformanceMetrics["ChamberPressure"].Should().Be(275.5);
        result.PerformanceMetrics["Iterations"].Should().Be(result.Iterations);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithThrustOnly_DoesNotReportThrustAsChamberPressure()
    {
        var result = await _engine.AnalyzeEngineAsync(
            "TestEngine",
            "CFD",
            new Dictionary<string, object>
            {
                ["thrust"] = 1234567,
                ["iterations"] = 40
            });

        result.ThrustAnalysis.MaxThrust.Should().Be(1234567);
        if (result.PerformanceMetrics.TryGetValue("ChamberPressure", out var chamberPressure))
        {
            chamberPressure.Should().NotBe(1234567);
        }
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithThermalParameters_AppliesTemperatureAndCooling()
    {
        var result = await _engine.AnalyzeEngineAsync(
            "TestEngine",
            "Thermal",
            new Dictionary<string, object>
            {
                ["maxTemperature"] = 4100,
                ["coolingEfficiency"] = 0.77,
                ["iterations"] = 55
            });

        result.SimulationType.Should().Be("Thermal");
        result.Iterations.Should().Be(120);
        result.ThermalAnalysis.MaxTemperature.Should().Be(4100);
        result.ThermalAnalysis.CoolingEfficiency.Should().BeApproximately(0.77, 0.0001);
        result.PerformanceMetrics["MaxTemperature"].Should().Be(4100);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithStructuralParameters_AppliesStressButNotClientSafetyFactor()
    {
        var baseline = HelloblueGKEngine.CreateDesignParametersFromEngine(
            thrust: 1_500_000,
            specificImpulse: 350,
            chamberPressure: 250,
            efficiency: 0.9);

        var honest = await _engine.AnalyzeEngineAsync(
            "TestEngine",
            "Structural",
            new Dictionary<string, object>
            {
                ["maxStress"] = 650e6,
                ["iterations"] = 66
            },
            baseline);
        var forged = await _engine.AnalyzeEngineAsync(
            "TestEngine",
            "Structural",
            new Dictionary<string, object>
            {
                ["maxStress"] = 650e6,
                ["safetyFactor"] = 100.0,
                ["iterations"] = 66
            },
            baseline);

        forged.SimulationType.Should().Be("Structural");
        forged.Iterations.Should().Be(100);
        forged.StructuralAnalysis.MaxStress.Should().Be(650e6);
        forged.StructuralAnalysis.SafetyFactor.Should().BeApproximately(
            honest.StructuralAnalysis.SafetyFactor,
            0.0001);
        forged.StructuralAnalysis.SafetyFactor.Should().BeLessThan(10.0);
        forged.PerformanceMetrics["MaxStress"].Should().Be(650e6);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithBaselineDesign_UsesEngineCharacteristicsForMultiPhysics()
    {
        var baseline = HelloblueGKEngine.CreateDesignParametersFromEngine(
            thrust: 2_500_000,
            specificImpulse: 420,
            chamberPressure: 300,
            efficiency: 0.93);

        var withBaseline = await _engine.AnalyzeEngineAsync(
            "BaselineEngine",
            "MultiPhysics",
            parameters: null,
            baseline);
        var withDefaults = await _engine.AnalyzeEngineAsync(
            "DefaultEngine",
            "MultiPhysics",
            parameters: null);

        withBaseline.SimulationType.Should().Be("MultiPhysics");
        withBaseline.OptimizationResult.OriginalParameters.Thrust.Should().Be(2_500_000);
        withBaseline.OptimizationResult.OriginalParameters.SpecificImpulse.Should().Be(420);
        withDefaults.OptimizationResult.OriginalParameters.Thrust.Should().Be(1_500_000);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithBaselineDesign_DifferentiatesCfdThermalStructuralPhysics()
    {
        var raptor = HelloblueGKEngine.CreateDesignParametersFromEngine(
            thrust: 2_200_000,
            specificImpulse: 350,
            chamberPressure: 300,
            efficiency: 0.95);
        var merlin = HelloblueGKEngine.CreateDesignParametersFromEngine(
            thrust: 845_000,
            specificImpulse: 282,
            chamberPressure: 97,
            efficiency: 0.88);

        var raptorCfd = await _engine.AnalyzeEngineAsync("Raptor", "CFD", null, raptor);
        var merlinCfd = await _engine.AnalyzeEngineAsync("Merlin", "CFD", null, merlin);
        var raptorThermal = await _engine.AnalyzeEngineAsync("Raptor", "Thermal", null, raptor);
        var merlinThermal = await _engine.AnalyzeEngineAsync("Merlin", "Thermal", null, merlin);
        var raptorStructural = await _engine.AnalyzeEngineAsync("Raptor", "Structural", null, raptor);
        var merlinStructural = await _engine.AnalyzeEngineAsync("Merlin", "Structural", null, merlin);

        raptorCfd.ThrustAnalysis.MaxThrust.Should().Be(2_200_000);
        merlinCfd.ThrustAnalysis.MaxThrust.Should().Be(845_000);
        raptorCfd.ThrustAnalysis.Efficiency.Should().BeApproximately(0.95, 0.0001);
        merlinCfd.ThrustAnalysis.Efficiency.Should().BeApproximately(0.88, 0.0001);
        raptorCfd.PerformanceMetrics["ChamberPressure"].Should().Be(300);
        merlinCfd.PerformanceMetrics["ChamberPressure"].Should().Be(97);

        raptorThermal.ThermalAnalysis.MaxTemperature
            .Should().BeGreaterThan(merlinThermal.ThermalAnalysis.MaxTemperature);
        raptorStructural.StructuralAnalysis.MaxStress
            .Should().BeGreaterThan(merlinStructural.StructuralAnalysis.MaxStress);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithClientAccuracyParameter_DoesNotOverwriteSolverTrustMetrics()
    {
        var baseline = HelloblueGKEngine.CreateDesignParametersFromEngine(
            thrust: 1_500_000,
            specificImpulse: 350,
            chamberPressure: 250,
            efficiency: 0.9);

        var forged = await _engine.AnalyzeEngineAsync(
            "TrustEngine",
            "CFD",
            new Dictionary<string, object>
            {
                ["accuracy"] = 0.01,
                ["efficiency"] = 0.5
            },
            baseline);
        var honest = await _engine.AnalyzeEngineAsync(
            "TrustEngine",
            "CFD",
            parameters: null,
            baseline);

        forged.ThrustAnalysis.Efficiency.Should().BeApproximately(0.5, 0.0001);
        forged.ConvergenceRate.Should().BeApproximately(honest.ConvergenceRate, 0.0001);
        forged.ConvergenceRate.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task AnalyzeEngineAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await _engine.AnalyzeEngineAsync(
            "CancelEngine",
            "CFD",
            parameters: null,
            baselineDesign: null,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act
        var action = () => _engine.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }
}

