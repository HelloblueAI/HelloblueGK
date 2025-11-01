using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class StructuredLoggingServiceTests
{
    private readonly Mock<ILogger<StructuredLoggingService>> _mockLogger;
    private readonly StructuredLoggingService _service;

    public StructuredLoggingServiceTests()
    {
        _mockLogger = new Mock<ILogger<StructuredLoggingService>>();
        _service = new StructuredLoggingService(_mockLogger.Object);
    }

    [Fact]
    public void LogEngineOperation_ShouldNotThrow()
    {
        // Arrange
        var operation = "TestOperation";
        var engineId = "TestEngine";
        var data = new { Test = "Data" };

        // Act
        var action = () => _service.LogEngineOperation(operation, engineId, data);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void LogPhysicsSimulation_ShouldNotThrow()
    {
        // Arrange
        var simulationType = "CFD";
        var engineId = "TestEngine";
        var data = new { Convergence = 0.99 };
        var duration = TimeSpan.FromSeconds(1);

        // Act
        var action = () => _service.LogPhysicsSimulation(simulationType, engineId, data, duration);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void BeginOperationScope_ShouldReturnDisposable()
    {
        // Arrange
        var operationName = "TestOperation";
        var operationId = Guid.NewGuid().ToString();

        // Act
        var scope = _service.BeginOperationScope(operationName, operationId);

        // Assert
        scope.Should().NotBeNull();
        scope.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void LogOperationComplete_ShouldNotThrow()
    {
        // Arrange
        var operationName = "TestOperation";
        var operationId = Guid.NewGuid().ToString();
        var duration = TimeSpan.FromSeconds(1);
        var success = true;
        var data = new { Result = "Success" };

        // Act
        var action = () => _service.LogOperationComplete(operationName, operationId, duration, success, data);

        // Assert
        action.Should().NotThrow();
    }
}

