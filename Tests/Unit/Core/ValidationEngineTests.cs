using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class ValidationEngineTests
{
    private readonly ValidationEngine _validationEngine;

    public ValidationEngineTests()
    {
        _validationEngine = new ValidationEngine();
    }

    [Fact]
    public async Task ValidateEngineAsync_ShouldReturnValidationResult()
    {
        // Arrange
        var engineModel = "TestEngine_001";

        // Act
        var result = await _validationEngine.ValidateEngineAsync(engineModel);

        // Assert
        result.Should().NotBeNull();
        result.EngineId.Should().Be(engineModel);
        result.TestResults.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateEngineAsync_ShouldHaveValidTestResults()
    {
        // Arrange
        var engineModel = "TestEngine_002";

        // Act
        var result = await _validationEngine.ValidateEngineAsync(engineModel);

        // Assert
        result.TestResults.Should().NotBeEmpty();
        result.TestResults.Should().ContainKey("Thrust");
        result.TestResults.Should().ContainKey("ISP");
        result.TestResults.Should().ContainKey("ChamberPressure");
    }

    [Fact]
    public async Task GenerateValidationSummaryAsync_ShouldReturnValidSummary()
    {
        // Act
        var summary = await _validationEngine.GenerateValidationSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.TotalEnginesValidated.Should().BeGreaterThanOrEqualTo(0);
        summary.AverageAccuracy.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetValidationHistoryAsync_ShouldReturnHistory()
    {
        // Arrange
        await _validationEngine.ValidateEngineAsync("Engine1");
        await _validationEngine.ValidateEngineAsync("Engine2");

        // Act
        var history = await _validationEngine.GetValidationHistoryAsync();

        // Assert
        history.Should().NotBeNull();
        history.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task IsEngineValidatedAsync_ShouldReturnTrueForValidatedEngine()
    {
        // Arrange
        var engineModel = "ValidatedEngine";
        await _validationEngine.ValidateEngineAsync(engineModel);

        // Act
        var isValidated = await _validationEngine.IsEngineValidatedAsync(engineModel);

        // Assert
        isValidated.Should().BeTrue();
    }

    [Fact]
    public async Task IsEngineValidatedAsync_ShouldReturnFalseForUnvalidatedEngine()
    {
        // Arrange
        var engineModel = "UnvalidatedEngine";

        // Act
        var isValidated = await _validationEngine.IsEngineValidatedAsync(engineModel);

        // Assert
        isValidated.Should().BeFalse();
    }

    [Fact]
    public void ValidateEngineModel_ShouldReturnValidationReport()
    {
        // Arrange
        var engineModel = "TestEngine";
        var simulationResults = new SimulationResults
        {
            Thrust = 1500000,
            SpecificImpulse = 380,
            ChamberPressure = 250
        };

        // Act
        var report = _validationEngine.ValidateEngineModel(engineModel, simulationResults);

        // Assert
        report.Should().NotBeNull();
        report.OverallAccuracy.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
    }
}

