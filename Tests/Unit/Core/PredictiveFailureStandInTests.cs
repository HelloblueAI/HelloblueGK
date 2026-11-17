using System;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Failure prediction used to publish a life, a mode, and an accuracy that were
/// never measured. Those fields stay unset, and an unmeasured probability does
/// not count as a reliable engine.
/// </summary>
public class PredictiveFailureStandInTests
{
    [Fact]
    public async Task PredictFailureAsync_DoesNotInventALifeOrMode()
    {
        var predictor = new PredictiveFailureSystem();
        var engine = new SelfOptimizedEngine { Id = "Revolutionary_Engine_1" };

        var first = await predictor.PredictFailureAsync(engine);
        var second = await predictor.PredictFailureAsync(engine);

        first.EngineId.Should().Be("Revolutionary_Engine_1");
        AssertUnmeasured(first);
        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task NamedPredictFailureAsync_DoesNotPublishThermalStressAt5000Hours()
    {
        var result = await new AutonomousEngineDesigner().PredictFailureAsync("engine-1");

        result.EngineId.Should().Be("engine-1");
        result.PredictionAccuracy.Should().Be(0);
        result.PredictedFailureMode.Should().BeEmpty();
        result.TimeToFailure.Should().Be(TimeSpan.Zero);
        result.ConfidenceLevel.Should().Be(0);
        result.RecommendedActions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NamedPredictFailureAsync_BlankId_StaysUnmeasured(string? engineId)
    {
        var result = await new AutonomousEngineDesigner().PredictFailureAsync(engineId!);

        result.EngineId.Should().BeEmpty();
        result.PredictionAccuracy.Should().Be(0);
        result.PredictedFailureMode.Should().BeEmpty();
        result.TimeToFailure.Should().Be(TimeSpan.Zero);
        result.ConfidenceLevel.Should().Be(0);
    }

    [Fact]
    public async Task RevolutionaryDesign_ReturnsUnmeasuredFailurePredictions()
    {
        var result = await new AutonomousEngineDesigner().GenerateRevolutionaryEngineAsync(new DesignRequirements());

        result.FailurePredictions.Should().NotBeEmpty();
        result.FailurePredictionAccuracy.Should().Be(0);
        result.BestEngine.Should().NotBeNull();
        result.FailurePredictions.Should().OnlyContain(prediction =>
            prediction.FailureProbability == 0
            && prediction.PredictedFailureMode == string.Empty
            && prediction.TimeToFailure == TimeSpan.Zero
            && prediction.ConfidenceLevel == 0
            && prediction.PredictionAccuracy == 0
            && prediction.RecommendedActions.Count == 0);
    }

    [Fact]
    public void ReliabilityCredit_IgnoresAnUnmeasuredProbability()
    {
        var unmeasured = new FailurePrediction
        {
            FailureProbability = 0,
            ConfidenceLevel = 0
        };
        AutonomousEngineDesigner.ReliabilityCredit(unmeasured).Should().Be(0);

        var measured = new FailurePrediction
        {
            FailureProbability = 0.2,
            ConfidenceLevel = 0.8
        };
        AutonomousEngineDesigner.ReliabilityCredit(measured).Should().BeApproximately(0.8, 1e-9);

        var notAProbability = new FailurePrediction
        {
            FailureProbability = 2,
            ConfidenceLevel = 0.9
        };
        AutonomousEngineDesigner.ReliabilityCredit(notAProbability).Should().Be(0);

        var notFinite = new FailurePrediction
        {
            FailureProbability = double.NaN,
            ConfidenceLevel = double.PositiveInfinity
        };
        AutonomousEngineDesigner.ReliabilityCredit(notFinite).Should().Be(0);
    }

    private static void AssertUnmeasured(FailurePrediction prediction)
    {
        prediction.FailureProbability.Should().Be(0);
        prediction.PredictedFailureMode.Should().BeEmpty();
        prediction.TimeToFailure.Should().Be(TimeSpan.Zero);
        prediction.ConfidenceLevel.Should().Be(0);
        prediction.PredictionAccuracy.Should().Be(0);
        prediction.RecommendedActions.Should().BeEmpty();
    }
}
