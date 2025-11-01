using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class DigitalTwinEngineTests : IDisposable
{
    private readonly DigitalTwinEngine _digitalTwinEngine;

    public DigitalTwinEngineTests()
    {
        _digitalTwinEngine = new DigitalTwinEngine();
    }

    [Fact]
    public async Task InitializeAsync_ShouldReturnValidStatus()
    {
        // Act
        var status = await _digitalTwinEngine.InitializeAsync();

        // Assert
        status.Should().NotBeNull();
        status.IsReady.Should().BeTrue();
        status.ActiveSystems.Should().NotBeEmpty();
        status.LearningMode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateDigitalTwinAsync_ShouldCreateValidTwin()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_001";
        var engineModel = new EngineModel
        {
            Name = "Test Engine",
            Parameters = new Dictionary<string, double>
            {
                ["Thrust"] = 1500000,
                ["ISP"] = 380
            }
        };

        // Act
        var twin = await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, engineModel);

        // Assert
        twin.Should().NotBeNull();
        twin.EngineId.Should().Be(engineId);
        twin.EngineModel.Should().NotBeNull();
        twin.PredictionAccuracy.Should().BeGreaterThan(0);
        twin.LearningStatus.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateDigitalTwinAsync_ShouldThrowOnNullEngineModel()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_002";

        // Act
        var action = async () => await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateDigitalTwinAsync_ShouldThrowOnEmptyEngineId()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineModel = new EngineModel { Name = "Test" };

        // Act
        var action = async () => await _digitalTwinEngine.CreateDigitalTwinAsync("", engineModel);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LearnFromTestFlightAsync_ShouldUpdateTwin()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_003";
        var engineModel = new EngineModel { Name = "Test Engine", Parameters = new Dictionary<string, double>() };
        await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, engineModel);

        var flightData = new TestFlightData
        {
            EngineId = engineId,
            FlightDate = DateTime.UtcNow,
            FlightMetrics = new Dictionary<string, double>
            {
                ["Thrust"] = 1500000,
                ["Efficiency"] = 0.92
            }
        };

        // Act
        var result = await _digitalTwinEngine.LearnFromTestFlightAsync(engineId, flightData);

        // Assert
        result.Should().NotBeNull();
        result.EngineId.Should().Be(engineId);
        result.LearningEvent.Should().NotBeNull();
        result.ModelImprovement.Should().NotBeNull();
    }

    [Fact]
    public async Task PredictEngineBehaviorAsync_ShouldReturnPrediction()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_004";
        var engineModel = new EngineModel { Name = "Test Engine", Parameters = new Dictionary<string, double>() };
        await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, engineModel);

        var scenario = new PredictionScenario
        {
            Name = "Test Scenario",
            Parameters = new Dictionary<string, object>()
        };

        // Act
        var prediction = await _digitalTwinEngine.PredictEngineBehaviorAsync(engineId, scenario);

        // Assert
        prediction.Should().NotBeNull();
        prediction.EngineId.Should().Be(engineId);
        prediction.ConfidenceLevel.Should().BeGreaterThan(0);
        prediction.PredictedMetrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateDigitalTwinSummaryAsync_ShouldReturnSummary()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineModel = new EngineModel { Name = "Test Engine", Parameters = new Dictionary<string, double>() };
        await _digitalTwinEngine.CreateDigitalTwinAsync("Engine1", engineModel);
        await _digitalTwinEngine.CreateDigitalTwinAsync("Engine2", engineModel);

        // Act
        var summary = await _digitalTwinEngine.GenerateDigitalTwinSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.TotalTwins.Should().BeGreaterThanOrEqualTo(2);
        summary.AveragePredictionAccuracy.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GenerateLearningPerformanceReportAsync_ShouldReturnReport()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_005";
        var engineModel = new EngineModel { Name = "Test Engine", Parameters = new Dictionary<string, double>() };
        await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, engineModel);

        // Act
        var report = await _digitalTwinEngine.GenerateLearningPerformanceReportAsync(engineId);

        // Assert
        report.Should().NotBeNull();
        report.EngineId.Should().Be(engineId);
        report.PredictionAccuracy.Should().BeGreaterThanOrEqualTo(0);
    }

    public void Dispose()
    {
        _digitalTwinEngine?.Dispose();
    }
}

