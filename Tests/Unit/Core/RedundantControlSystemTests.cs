using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.Core.Control;
using HB_NLP_Research_Lab.Core.Hardware;
using Moq;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

public class RedundantControlSystemTests
{
    /// <summary>
    /// Control loop that records whether StartAsync was invoked.
    /// </summary>
    private sealed class SpyControlLoop : RealTimeControlLoop
    {
        public int StartCallCount { get; private set; }

        public SpyControlLoop(int frequencyHz = 10) : base(frequencyHz) { }

        public override Task StartAsync()
        {
            StartCallCount++;
            return base.StartAsync();
        }

        protected override Task ExecuteControlLoopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Mock<IActuator> CreateActuatorMock(string id)
    {
        var actuator = new Mock<IActuator>(MockBehavior.Strict);
        actuator.Setup(a => a.ActuatorId).Returns(id);
        actuator.Setup(a => a.Name).Returns(id);
        actuator.Setup(a => a.Type).Returns(ActuatorType.Throttle);
        actuator.Setup(a => a.Status).Returns(ActuatorStatus.Ready);
        actuator.Setup(a => a.MinPosition).Returns(0);
        actuator.Setup(a => a.MaxPosition).Returns(1);
        actuator.Setup(a => a.ResponseTimeSeconds).Returns(0.1);
        actuator.Setup(a => a.MaxRateOfChange).Returns(1);
        actuator.Setup(a => a.IsEnabled).Returns(true);
        return actuator;
    }

    private static RedundantControlSystem CreateSystem(
        out SpyControlLoop loopA,
        out SpyControlLoop loopB,
        out Mock<IActuator> primary,
        out Mock<IActuator> redundant)
    {
        loopA = new SpyControlLoop();
        loopB = new SpyControlLoop();
        primary = CreateActuatorMock("primary");
        redundant = CreateActuatorMock("redundant");

        return new RedundantControlSystem(
            new List<RealTimeControlLoop> { loopA, loopB },
            VotingStrategy.MajorityVote,
            primary.Object,
            new List<IActuator> { redundant.Object });
    }

    [Fact]
    public async Task StartAsync_ThrowsNotSupportedException_WithClearMessage()
    {
        using var system = CreateSystem(out _, out _, out var primary, out var redundant);

        var act = async () => await system.StartAsync();

        var exception = await act.Should().ThrowAsync<NotSupportedException>();
        exception.Which.Message.Should().Contain("disabled");
        exception.Which.Message.Should().Contain("per-channel output sources");

        primary.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
        redundant.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StartAsync_DoesNotSetIsRunning_OrStartLoops_OrCommandActuators()
    {
        using var system = CreateSystem(out var loopA, out var loopB, out var primary, out var redundant);

        var act = async () => await system.StartAsync();
        await act.Should().ThrowAsync<NotSupportedException>();

        system.IsRunning.Should().BeFalse();
        loopA.StartCallCount.Should().Be(0);
        loopB.StartCallCount.Should().Be(0);
        loopA.IsRunning.Should().BeFalse();
        loopB.IsRunning.Should().BeFalse();

        primary.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
        redundant.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StopAsync_AndDispose_AfterFailedStart_AreSafe_AndDoNotCommandActuators()
    {
        var system = CreateSystem(out _, out _, out var primary, out var redundant);

        var act = async () => await system.StartAsync();
        await act.Should().ThrowAsync<NotSupportedException>();

        var stop = async () => await system.StopAsync();
        await stop.Should().NotThrowAsync();
        system.IsRunning.Should().BeFalse();

        var disposeOnce = () => system.Dispose();
        disposeOnce.Should().NotThrow();

        var disposeTwice = () => system.Dispose();
        disposeTwice.Should().NotThrow();

        primary.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
        redundant.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StopAsync_AndDispose_WhenNeverStarted_AreSafe_AndDoNotCommandActuators()
    {
        var system = CreateSystem(out var loopA, out var loopB, out var primary, out var redundant);

        var stop = async () => await system.StopAsync();
        await stop.Should().NotThrowAsync();
        system.IsRunning.Should().BeFalse();
        loopA.StartCallCount.Should().Be(0);
        loopB.StartCallCount.Should().Be(0);

        system.Dispose();
        system.Dispose();

        primary.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
        redundant.Verify(
            a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_RejectsFewerThanTwoControlLoops()
    {
        var primary = CreateActuatorMock("primary");
        var singleLoop = new List<RealTimeControlLoop> { new SpyControlLoop() };

        var act = () => new RedundantControlSystem(
            singleLoop,
            VotingStrategy.MajorityVote,
            primary.Object);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("controlLoops");
    }

    [Theory]
    [InlineData(0.50, 0.50, 0.50, 0.50)]
    [InlineData(0.50, 0.50, 0.10, 0.50)]
    public void MajorityVote_CommandsAStrictMajority(double first, double second, double third, double expected)
    {
        RedundantControlSystem.MajorityVote(new[] { first, second, third }).Should().Be(expected);
    }

    [Fact]
    public void MajorityVote_HoldsWhenThereIsNoMajority()
    {
        RedundantControlSystem.MajorityVote(new[] { 0.10, 0.20, 0.30 }).Should().BeNull();
        RedundantControlSystem.MajorityVote(new[] { 0.50, double.NaN, double.NaN }).Should().BeNull();
        RedundantControlSystem.MajorityVote(new[] { double.NaN, double.NaN, double.NaN }).Should().BeNull();
    }

    [Fact]
    public void MajorityVote_TwoHealthyChannelsOutvoteOneFailedChannel()
    {
        RedundantControlSystem.MajorityVote(new[] { 0.50, 0.50, double.NaN }).Should().Be(0.50);
        RedundantControlSystem.MajorityVote(new[] { 0.50, 0.50, double.PositiveInfinity }).Should().Be(0.50);
        RedundantControlSystem.MajorityVote(new[] { 0.50, 0.50, double.NegativeInfinity }).Should().Be(0.50);
    }

    [Fact]
    public void ConsensusVote_AgreesWithinToleranceAndHoldsOnAFailedChannel()
    {
        RedundantControlSystem.ConsensusVote(new[] { 0.50, 0.50, 0.50 }).Should().Be(0.50);
        RedundantControlSystem.ConsensusVote(new[] { 0.0, 0.25 }).Should().BeNull();
        RedundantControlSystem.ConsensusVote(new[] { 0.50, 0.50, double.NaN }).Should().BeNull();
        RedundantControlSystem.ConsensusVote(new[] { 0.50, 0.50, double.PositiveInfinity }).Should().BeNull();
        RedundantControlSystem.ConsensusVote(new[] { 0.50, 0.50, double.NegativeInfinity }).Should().BeNull();
    }

    [Fact]
    public void AverageVote_HoldsWhenAnyChannelIsNonFinite()
    {
        RedundantControlSystem.AverageVote(new[] { 0.25, 0.75 }).Should().Be(0.50);
        RedundantControlSystem.AverageVote(new[] { 0.25, double.NaN }).Should().BeNull();
        RedundantControlSystem.AverageVote(new[] { 0.25, double.PositiveInfinity }).Should().BeNull();
    }

    [Fact]
    public void MedianAndMidValue_HoldWhenTheSelectedChannelIsNonFinite()
    {
        RedundantControlSystem.MedianVote(new[] { 0.10, 0.20, 0.90 }).Should().Be(0.20);
        RedundantControlSystem.MedianVote(new[] { 0.25, 0.75 }).Should().Be(0.50);
        RedundantControlSystem.MedianVote(new[] { 0.50, 0.50, double.NaN }).Should().Be(0.50);
        RedundantControlSystem.MedianVote(new[] { double.NaN, double.NaN, 0.50 }).Should().BeNull();

        RedundantControlSystem.MidValueSelect(new[] { 0.10, 0.90, 0.20 }).Should().Be(0.20);
        RedundantControlSystem.MidValueSelect(new[] { double.NaN, double.NaN, 0.50 }).Should().BeNull();
    }

    [Fact]
    public void DetectChannelFaults_ReportsANonFiniteChannel()
    {
        var faults = RedundantControlSystem.DetectChannelFaults(new[] { 0.50, 0.50, double.NaN });

        faults.Should().ContainSingle();
        faults[0].ControllerIndex.Should().Be(2);
        faults[0].OutputValue.Should().Be(double.NaN);
        double.IsPositiveInfinity(faults[0].Deviation).Should().BeTrue();
    }

    [Fact]
    public void DetectChannelFaults_StillReportsAFiniteOutlier()
    {
        // A single extreme point beats three sample standard deviations only once
        // the channel count is large enough for that bound.
        var outputs = new double[12];
        for (var i = 0; i < outputs.Length - 1; i++)
        {
            outputs[i] = 0.50;
        }

        outputs[^1] = 10.0;

        var faults = RedundantControlSystem.DetectChannelFaults(outputs);

        faults.Should().ContainSingle();
        faults[0].ControllerIndex.Should().Be(outputs.Length - 1);
        faults[0].OutputValue.Should().Be(10.0);
    }

    [Fact]
    public void SelectVote_DoesNotCommandNaNForAnyStrategy()
    {
        var failed = new[] { 0.40, double.NaN, 0.40 };
        foreach (VotingStrategy strategy in Enum.GetValues<VotingStrategy>())
        {
            var command = RedundantControlSystem.SelectVote(strategy, failed);
            if (command.HasValue)
            {
                double.IsFinite(command.Value).Should().BeTrue();
            }
        }
    }
}
