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
        result.Iterations.Should().Be(42);
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

