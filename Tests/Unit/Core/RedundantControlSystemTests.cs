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
    /// Minimal concrete control loop for testing (no sensors/actuators).
    /// </summary>
    private sealed class TestControlLoop : RealTimeControlLoop
    {
        public TestControlLoop(int frequencyHz = 10) : base(frequencyHz) { }
        protected override Task ExecuteControlLoopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task StopAsync_WaitsForMonitoringTask_OrTimesOut()
    {
        var primaryActuator = new Mock<IActuator>();
        primaryActuator.Setup(a => a.SetPositionAsync(It.IsAny<double>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        primaryActuator.Setup(a => a.ActuatorId).Returns("primary");
        primaryActuator.Setup(a => a.Name).Returns("Primary");
        primaryActuator.Setup(a => a.Type).Returns(ActuatorType.Throttle);
        primaryActuator.Setup(a => a.Status).Returns(ActuatorStatus.Ready);
        primaryActuator.Setup(a => a.MinPosition).Returns(0);
        primaryActuator.Setup(a => a.MaxPosition).Returns(1);
        primaryActuator.Setup(a => a.ResponseTimeSeconds).Returns(0.1);
        primaryActuator.Setup(a => a.MaxRateOfChange).Returns(1);
        primaryActuator.Setup(a => a.IsEnabled).Returns(true);
        primaryActuator.Setup(a => a.GetPositionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0.5);
        primaryActuator.Setup(a => a.EnableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        primaryActuator.Setup(a => a.DisableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var loops = new List<RealTimeControlLoop> { new TestControlLoop(10), new TestControlLoop(10) };
        using var system = new RedundantControlSystem(
            loops,
            VotingStrategy.MajorityVote,
            primaryActuator.Object);

        await system.StartAsync();
        await Task.Delay(80);
        await system.StopAsync();

        system.IsRunning.Should().BeFalse();
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
