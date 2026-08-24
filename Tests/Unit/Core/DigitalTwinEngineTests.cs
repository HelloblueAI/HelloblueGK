using FluentAssertions;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.AI;

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
        twin.PredictionAccuracy.Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
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
        prediction.ConfidenceLevel.Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
        prediction.PredictedMetrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PredictEngineBehaviorAsync_IgnoresClientReliabilityOverride()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_ReliabilityForge";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel
            {
                Name = "Reliability Engine",
                Parameters = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_500_000,
                    ["Efficiency"] = 0.92
                }
            });

        var baseline = await _digitalTwinEngine.PredictEngineBehaviorAsync(
            engineId,
            new PredictionScenario
            {
                Name = "Baseline",
                Parameters = new Dictionary<string, object>()
            });
        var forged = await _digitalTwinEngine.PredictEngineBehaviorAsync(
            engineId,
            new PredictionScenario
            {
                Name = "ForgedReliability",
                Parameters = new Dictionary<string, object>
                {
                    ["reliability"] = 1.0
                }
            });

        forged.PredictedMetrics["Reliability"].Should().BeApproximately(
            baseline.PredictedMetrics["Reliability"],
            0.0001);
        baseline.PredictedMetrics["Reliability"].Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
        forged.PredictedMetrics["Reliability"].Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
        forged.ConfidenceLevel.Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
    }

    [Fact]
    public async Task LearnFromTestFlightAsync_UpdatesAccuracyFromFlightResiduals()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_AccuracyResiduals";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel
            {
                Name = "Residual Engine",
                Parameters = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000,
                    ["Efficiency"] = 0.90,
                    ["ChamberPressure"] = 250,
                    ["Reliability"] = 0.95
                }
            });

        var matching = await _digitalTwinEngine.LearnFromTestFlightAsync(
            engineId,
            new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000,
                    ["Efficiency"] = 0.90,
                    ["ChamberPressure"] = 250,
                    ["Reliability"] = 0.95
                }
            });
        var mismatched = await _digitalTwinEngine.LearnFromTestFlightAsync(
            engineId,
            new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double>
                {
                    ["Thrust"] = 500_000,
                    ["Efficiency"] = 0.45,
                    ["ChamberPressure"] = 100,
                    ["Reliability"] = 0.40
                }
            });

        // Complete perfect match is EMA-blended with unproven prior (0.25*1 + 0.75*0.5 = 0.625).
        matching.UpdatedPredictionAccuracy.OverallAccuracy.Should().BeApproximately(0.625, 0.0001);
        matching.UpdatedPredictionAccuracy.ThrustPredictionAccuracy.Should().BeApproximately(1.0, 0.0001);
        matching.UpdatedPredictionAccuracy.ThermalPredictionAccuracy.Should().BeApproximately(1.0, 0.0001);
        matching.UpdatedPredictionAccuracy.StructuralPredictionAccuracy.Should().BeApproximately(1.0, 0.0001);
        matching.UpdatedPredictionAccuracy.FailurePredictionAccuracy.Should().BeApproximately(1.0, 0.0001);

        mismatched.UpdatedPredictionAccuracy.OverallAccuracy.Should().BeLessThan(0.6);
        mismatched.UpdatedPredictionAccuracy.OverallAccuracy
            .Should().BeLessThan(matching.UpdatedPredictionAccuracy.OverallAccuracy);
    }

    [Fact]
    public async Task LearnFromTestFlightAsync_PartialTelemetryEcho_CannotForgeHighAccuracy()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_PartialEchoForge";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel
            {
                Name = "Echo Engine",
                Parameters = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000,
                    ["Efficiency"] = 0.90,
                    ["ChamberPressure"] = 250,
                    ["Reliability"] = 0.95
                }
            });

        // Classic Admin echo forge: predict-model values for only two metrics.
        var echo = await _digitalTwinEngine.LearnFromTestFlightAsync(
            engineId,
            new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000,
                    ["Efficiency"] = 0.90
                }
            });

        echo.UpdatedPredictionAccuracy.OverallAccuracy
            .Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
        echo.UpdatedPredictionAccuracy.ThrustPredictionAccuracy.Should().BeApproximately(1.0, 0.0001);
        echo.UpdatedPredictionAccuracy.StructuralPredictionAccuracy
            .Should().Be(DigitalTwinEngine.UnprovenPredictionAccuracy);
    }

    [Fact]
    public async Task LearnFromTestFlightAsync_SinglePerfectEcho_CannotReachFullAccuracy()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_SingleEchoCap";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel
            {
                Name = "Full Echo Engine",
                Parameters = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000,
                    ["Efficiency"] = 0.90,
                    ["ChamberPressure"] = 250,
                    ["Reliability"] = 0.95
                }
            });

        var echo = await _digitalTwinEngine.LearnFromTestFlightAsync(
            engineId,
            new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000,
                    ["Efficiency"] = 0.90,
                    ["ChamberPressure"] = 250,
                    ["Reliability"] = 0.95
                }
            });

        echo.UpdatedPredictionAccuracy.OverallAccuracy.Should().BeLessThan(0.7);
        echo.UpdatedPredictionAccuracy.OverallAccuracy
            .Should().BeGreaterThan(DigitalTwinEngine.UnprovenPredictionAccuracy);
    }

    [Fact]
    public async Task PredictEngineBehaviorAsync_ShouldApplyScenarioParameters()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineId = "TestEngine_ScenarioParams";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "Scenario Engine", Parameters = new Dictionary<string, double>() });

        var baseline = await _digitalTwinEngine.PredictEngineBehaviorAsync(
            engineId,
            new PredictionScenario
            {
                Name = "Baseline",
                Parameters = new Dictionary<string, object>()
            });
        var throttled = await _digitalTwinEngine.PredictEngineBehaviorAsync(
            engineId,
            new PredictionScenario
            {
                Name = "Throttled",
                Parameters = new Dictionary<string, object>
                {
                    ["throttle"] = 0.8,
                    ["ambientTemperature"] = 320.0
                }
            });

        throttled.PredictedMetrics["Thrust"].Should().BeApproximately(
            baseline.PredictedMetrics["Thrust"] * 0.8,
            0.0001);
        throttled.PredictedMetrics["Efficiency"].Should().BeLessThan(baseline.PredictedMetrics["Efficiency"]);
    }

    [Fact]
    public async Task PredictEngineBehaviorAsync_ShouldSeedDefaultsFromEngineModel()
    {
        await _digitalTwinEngine.InitializeAsync();

        await _digitalTwinEngine.CreateDigitalTwinAsync(
            "RaptorTwin",
            new EngineModel
            {
                Name = "Raptor",
                Parameters = new Dictionary<string, double>
                {
                    ["Thrust"] = 2_200_000,
                    ["Efficiency"] = 0.95
                }
            });
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            "MerlinTwin",
            new EngineModel
            {
                Name = "Merlin",
                Parameters = new Dictionary<string, double>
                {
                    ["Thrust"] = 845_000,
                    ["Efficiency"] = 0.88
                }
            });

        var emptyScenario = new PredictionScenario
        {
            Name = "Nominal",
            Parameters = new Dictionary<string, object>()
        };

        var raptor = await _digitalTwinEngine.PredictEngineBehaviorAsync("RaptorTwin", emptyScenario);
        var merlin = await _digitalTwinEngine.PredictEngineBehaviorAsync("MerlinTwin", emptyScenario);

        raptor.PredictedMetrics["Thrust"].Should().Be(2_200_000);
        merlin.PredictedMetrics["Thrust"].Should().Be(845_000);
        raptor.PredictedMetrics["Efficiency"].Should().BeApproximately(0.95, 0.0001);
        merlin.PredictedMetrics["Efficiency"].Should().BeApproximately(0.88, 0.0001);
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

    [Fact]
    public async Task PredictAndLearn_CapsHistoryUnderRepeatedOperations()
    {
        await _digitalTwinEngine.InitializeAsync();
        const string engineId = "BoundedHistoryEngine";
        await _digitalTwinEngine.CreateDigitalTwinAsync(engineId, new EngineModel
        {
            Name = "Bounded History Engine",
            Parameters = new Dictionary<string, double>()
        });

        var operations = DigitalTwinEngine.MaxHistoryEntries + 40;
        for (var index = 0; index < operations; index++)
        {
            await _digitalTwinEngine.PredictEngineBehaviorAsync(engineId, new PredictionScenario
            {
                Name = $"Scenario_{index}",
                Parameters = new Dictionary<string, object>()
            });

            await _digitalTwinEngine.LearnFromTestFlightAsync(engineId, new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double>
                {
                    ["Thrust"] = 1_000_000 + index,
                    ["Efficiency"] = 0.9
                }
            });
        }

        var report = await _digitalTwinEngine.GenerateLearningPerformanceReportAsync(engineId);
        report.TotalPredictions.Should().Be(DigitalTwinEngine.MaxHistoryEntries);
        report.TotalLearningEvents.Should().Be(DigitalTwinEngine.MaxHistoryEntries);
        report.TotalModelImprovements.Should().Be(DigitalTwinEngine.MaxHistoryEntries);
    }

    [Fact]
    public async Task ConcurrentLearningAndPredictions_ShouldPreserveCompleteHistory()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        const string engineId = "ConcurrentEngine";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "Concurrent Test Engine", Parameters = new Dictionary<string, double>() });

        const int operationCount = 32;
        var learningTasks = Enumerable.Range(0, operationCount)
            .Select(index => _digitalTwinEngine.LearnFromTestFlightAsync(
                engineId,
                new TestFlightData
                {
                    EngineId = engineId,
                    FlightDate = DateTime.UtcNow,
                    FlightMetrics = new Dictionary<string, double> { ["Sequence"] = index }
                }));
        var predictionTasks = Enumerable.Range(0, operationCount)
            .Select(index => _digitalTwinEngine.PredictEngineBehaviorAsync(
                engineId,
                new PredictionScenario
                {
                    Name = $"Concurrent scenario {index}",
                    Parameters = new Dictionary<string, object>()
                }));

        // Act
        await Task.WhenAll(learningTasks.Cast<Task>().Concat(predictionTasks));
        var report = await _digitalTwinEngine.GenerateLearningPerformanceReportAsync(engineId);
        var summary = await _digitalTwinEngine.GenerateDigitalTwinSummaryAsync();

        // Assert
        report.TotalLearningEvents.Should().Be(operationCount);
        report.TotalModelImprovements.Should().Be(operationCount);
        report.TotalPredictions.Should().Be(operationCount);
        summary.TotalLearningEvents.Should().Be(operationCount);
        summary.TotalPredictions.Should().Be(operationCount);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreates_ShouldReplaceWholeTwinGeneration()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        const string engineId = "StableEngine";
        var originalTwin = await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "Original Engine", Parameters = new Dictionary<string, double>() });
        await _digitalTwinEngine.LearnFromTestFlightAsync(
            engineId,
            new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double> { ["Thrust"] = 1_500_000 }
            });

        // Act
        var duplicateCreates = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(index => _digitalTwinEngine.CreateDigitalTwinAsync(
                    engineId,
                    new EngineModel
                    {
                        Name = $"Replacement {index}",
                        Parameters = new Dictionary<string, double>()
                    })));
        var report = await _digitalTwinEngine.GenerateLearningPerformanceReportAsync(engineId);

        // Assert
        duplicateCreates.Should().OnlyContain(twin => !ReferenceEquals(twin, originalTwin));
        duplicateCreates.Distinct().Should().HaveCount(16);
        report.TotalLearningEvents.Should().Be(0);
        report.TotalModelImprovements.Should().Be(0);
    }

    [Fact]
    public async Task RemoveDigitalTwin_ShouldDropRuntimeState()
    {
        await _digitalTwinEngine.InitializeAsync();
        const string engineId = "RemovableEngine";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "Removable", Parameters = new Dictionary<string, double>() });

        _digitalTwinEngine.RemoveDigitalTwin(engineId).Should().BeTrue();
        _digitalTwinEngine.RemoveDigitalTwin(engineId).Should().BeFalse();

        var learn = async () => await _digitalTwinEngine.LearnFromTestFlightAsync(
            engineId,
            new TestFlightData
            {
                EngineId = engineId,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double> { ["Thrust"] = 1 }
            });

        await learn.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateDigitalTwinAsync_WhenAtCapacity_EvictsOldestTwin()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineModel = new EngineModel
        {
            Name = "Capacity Engine",
            Parameters = new Dictionary<string, double>()
        };

        for (var i = 0; i < DigitalTwinEngine.MaxActiveTwins; i++)
        {
            await _digitalTwinEngine.CreateDigitalTwinAsync($"CapacityEngine_{i}", engineModel);
        }

        await _digitalTwinEngine.CreateDigitalTwinAsync("CapacityEngine_overflow", engineModel);

        // Oldest key should have been evicted to keep the active set bounded.
        _digitalTwinEngine.RemoveDigitalTwin("CapacityEngine_0").Should().BeFalse();
        _digitalTwinEngine.RemoveDigitalTwin("CapacityEngine_overflow").Should().BeTrue();

        var status = await _digitalTwinEngine.InitializeAsync();
        status.TwinCount.Should().BeLessThanOrEqualTo(DigitalTwinEngine.MaxActiveTwins);
        // Evicted twin gates must be forgettable so create→evict churn cannot unbounded-grow.
        status.GateCount.Should().BeLessThanOrEqualTo(DigitalTwinEngine.MaxActiveTwins);
    }

    [Fact]
    public async Task RemoveDigitalTwin_ShouldDropIdleEngineGate()
    {
        await _digitalTwinEngine.InitializeAsync();
        const string engineId = "GateCleanupEngine";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "GateCleanup", Parameters = new Dictionary<string, double>() });

        (await _digitalTwinEngine.InitializeAsync()).GateCount.Should().Be(1);
        _digitalTwinEngine.RemoveDigitalTwin(engineId).Should().BeTrue();
        (await _digitalTwinEngine.InitializeAsync()).GateCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateDigitalTwinAsync_WhenAtCapacity_InPlaceUpdateDoesNotEvict()
    {
        await _digitalTwinEngine.InitializeAsync();
        var engineModel = new EngineModel
        {
            Name = "Capacity Engine",
            Parameters = new Dictionary<string, double>()
        };

        for (var i = 0; i < DigitalTwinEngine.MaxActiveTwins; i++)
        {
            await _digitalTwinEngine.CreateDigitalTwinAsync($"CapacityEngine_{i}", engineModel);
        }

        // Updating an existing key must not grow the map or evict another twin.
        await _digitalTwinEngine.CreateDigitalTwinAsync("CapacityEngine_1", engineModel);

        _digitalTwinEngine.RemoveDigitalTwin("CapacityEngine_0").Should().BeTrue();
        var status = await _digitalTwinEngine.InitializeAsync();
        status.TwinCount.Should().Be(DigitalTwinEngine.MaxActiveTwins - 1);
    }

    [Fact]
    public async Task Dispose_ShouldRejectFurtherOperations()
    {
        // Act
        _digitalTwinEngine.Dispose();
        var action = () => _digitalTwinEngine.InitializeAsync();

        // Assert
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeDuringInitialization_ShouldNotReturnReady()
    {
        // Act
        var initialization = _digitalTwinEngine.InitializeAsync();
        await Task.Delay(50);
        _digitalTwinEngine.Dispose();

        // Assert
        var action = async () => await initialization;
        await action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ForceCreateDuringAutonomousTest_ShouldNotApplyOldResultsToReplacement()
    {
        // Arrange
        await _digitalTwinEngine.InitializeAsync();
        const string engineId = "AutonomousReplacementEngine";
        await _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "Original Engine", Parameters = new Dictionary<string, double>() });

        // Act
        var autonomousTest = _digitalTwinEngine.RunAutonomousTestsAsync(
            engineId,
            new TestingRequirements { TestType = "Regression" });
        await Task.Delay(50);
        var replacement = _digitalTwinEngine.CreateDigitalTwinAsync(
            engineId,
            new EngineModel { Name = "Replacement Engine", Parameters = new Dictionary<string, double>() });
        await Task.WhenAll(autonomousTest, replacement);
        var report = await _digitalTwinEngine.GenerateLearningPerformanceReportAsync(engineId);

        // Assert
        report.TotalLearningEvents.Should().Be(0);
        report.TotalModelImprovements.Should().Be(0);
    }

    public void Dispose()
    {
        _digitalTwinEngine?.Dispose();
    }
}

