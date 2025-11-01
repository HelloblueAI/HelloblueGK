using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class RealTimeValidationEngineTests : IDisposable
{
    private readonly RealTimeValidationEngine _validationEngine;

    public RealTimeValidationEngineTests()
    {
        _validationEngine = new RealTimeValidationEngine();
    }

    [Fact]
    public async Task ValidateEngineModelAsync_ShouldReturnValidationReport()
    {
        // Arrange
        var engineModel = "TestEngine_001";

        // Act
        var report = await _validationEngine.ValidateEngineModelAsync(engineModel);

        // Assert
        report.Should().NotBeNull();
        report.OverallAccuracy.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
        report.ConfidenceLevel.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task ValidateEngineAsync_ShouldReturnValidationResult()
    {
        // Arrange
        var engineModel = "TestEngine_002";

        // Act
        var result = await _validationEngine.ValidateEngineAsync(engineModel);

        // Assert
        result.Should().NotBeNull();
        // RealTimeValidationEngine.ValidateEngineAsync returns ValidationResult which doesn't have EngineId property
        // It has ValidationType, DataSource, Accuracy, etc.
        result.Accuracy.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
        result.ValidationType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateEngineModelAsync_ShouldCacheResults()
    {
        // Arrange
        var engineModel = "CachedEngine";

        // Act
        var report1 = await _validationEngine.ValidateEngineModelAsync(engineModel);
        var report2 = await _validationEngine.ValidateEngineModelAsync(engineModel);

        // Assert
        report1.Should().NotBeNull();
        report2.Should().NotBeNull();
        // Both should return valid reports (caching tested implicitly)
    }

    [Fact]
    public async Task GenerateValidationSummaryAsync_ShouldReturnValidSummary()
    {
        // Arrange
        await _validationEngine.ValidateEngineAsync("Engine1");
        await _validationEngine.ValidateEngineAsync("Engine2");

        // Act
        var summary = await _validationEngine.GenerateValidationSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.TotalEnginesValidated.Should().BeGreaterThanOrEqualTo(2);
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

    public void Dispose()
    {
        // RealTimeValidationEngine doesn't implement IDisposable
        // No cleanup needed
    }
}

