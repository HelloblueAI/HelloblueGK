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
}
