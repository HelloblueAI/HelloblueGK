using HB_NLP_Research_Lab.Core.Control;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HelloblueGK.Tests.Unit.Core;

public class ThrottleControllerTests
{
    [Fact]
    public void NormalizedThrustError_IsAFractionOfTheTarget()
    {
        // 100 N short of 1 MN is 0.01 percent. The proportional gain is 0.5, so the
        // old Newton-scale error commanded 50 throttle units for that miss.
        var error = ThrottleController.NormalizedThrustError(999_900, 1_000_000);

        error.Should().BeApproximately(0.0001, 1e-12);
        (0.5 * error).Should().BeLessThan(0.001);
        (0.5 * (1_000_000 - 999_900)).Should().BeGreaterThan(1);
    }

    [Fact]
    public void NormalizedThrustError_OverThrust_IsNegative()
    {
        ThrottleController.NormalizedThrustError(1_100_000, 1_000_000)
            .Should().BeApproximately(-0.1, 1e-12);
    }

    [Theory]
    [InlineData(double.NaN, 1_000_000)]
    [InlineData(1_000_000, double.NaN)]
    [InlineData(double.PositiveInfinity, 1_000_000)]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    public void NormalizedThrustError_MissingOrNonPositiveTarget_ContributesNoError(
        double currentThrust,
        double targetThrust)
    {
        ThrottleController.NormalizedThrustError(currentThrust, targetThrust).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.4)]
    [InlineData(1)]
    public void SetThrottle_AcceptsAFiniteCommandInsideTheRange(double throttle)
    {
        var controller = CreateController();

        controller.SetThrottle(throttle);

        controller.CommandedThrottle.Should().Be(throttle);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void SetThrottle_RejectsANonFiniteOrOutOfRangeCommand(double throttle)
    {
        var controller = CreateController();
        controller.SetThrottle(0.4);

        var act = () => controller.SetThrottle(throttle);

        act.Should().Throw<ArgumentOutOfRangeException>();
        controller.CommandedThrottle.Should().Be(0.4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1_000_000)]
    public void SetTargetThrust_AcceptsAFiniteNonNegativeTarget(double targetThrust)
    {
        var controller = CreateController();

        controller.SetTargetThrust(targetThrust);

        controller.TargetThrust.Should().Be(targetThrust);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void SetTargetThrust_RejectsANonFiniteOrNegativeTarget(double targetThrust)
    {
        var controller = CreateController();
        controller.SetTargetThrust(1_000_000);

        var act = () => controller.SetTargetThrust(targetThrust);

        act.Should().Throw<ArgumentOutOfRangeException>();
        controller.TargetThrust.Should().Be(1_000_000);
    }

    [Fact]
    public void HoldOrStep_NonFiniteTarget_HoldsTheLastFinitePosition()
    {
        ThrottleController.HoldOrStep(0.4, double.NaN, 0.001).Should().Be(0.4);
        ThrottleController.HoldOrStep(0.4, double.PositiveInfinity, 0.001).Should().Be(0.4);
    }

    [Fact]
    public void HoldOrStep_NonFiniteCurrent_DoesNotSkipTheRateLimit()
    {
        ThrottleController.HoldOrStep(double.NaN, 0.5, 0.001).Should().BeApproximately(0.001, 1e-12);
        ThrottleController.HoldOrStep(double.NaN, double.NaN, 0.001).Should().Be(0);
    }

    [Fact]
    public void HoldOrStep_LimitsTheStepInBothDirections()
    {
        ThrottleController.HoldOrStep(0, 0.5, 0.001).Should().BeApproximately(0.001, 1e-12);
        ThrottleController.HoldOrStep(0, 0.0005, 0.001).Should().BeApproximately(0.0005, 1e-12);
        ThrottleController.HoldOrStep(0.5, 0, 0.001).Should().BeApproximately(0.499, 1e-12);
    }

    [Fact]
    public void ConstrainThrottleCommand_ClampsANonFiniteCommandToClosed()
    {
        var controller = CreateController();

        controller.ConstrainThrottleCommand(double.NaN).Should().Be(0);
        controller.ConstrainThrottleCommand(double.PositiveInfinity).Should().Be(0);
        controller.ConstrainThrottleCommand(-1).Should().Be(0);
        controller.ConstrainThrottleCommand(0.5).Should().BeInRange(0, 1);
        double.IsFinite(controller.ConstrainThrottleCommand(0.5)).Should().BeTrue();
    }

    [Fact]
    public async Task Stop_ClosesTheThrottleAndRateLimitsFromClosed()
    {
        var actuator = new RecordingActuator();
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);

        await controller.StartAsync();
        var opened = await WaitUntilAsync(
            () => controller.CurrentThrottle >= 0.02,
            TimeSpan.FromSeconds(5));
        opened.Should().BeTrue();
        var openPosition = controller.CurrentThrottle;

        await controller.StopAsync();

        controller.CurrentThrottle.Should().Be(0);
        actuator.LastPosition.Should().Be(0);
        // 0.1 per second at 100 Hz is a 0.001 step. The next command must start
        // from closed, not from the position held before the stop.
        var next = controller.ConstrainThrottleCommand(1);
        next.Should().BeApproximately(0.001, 1e-12);
        next.Should().BeLessThan(openPosition);
        controller.Dispose();
    }

    [Theory]
    [InlineData(SensorStatus.Ready, true)]
    [InlineData(SensorStatus.Unknown, false)]
    [InlineData(SensorStatus.Initializing, false)]
    [InlineData(SensorStatus.Reading, false)]
    [InlineData(SensorStatus.Error, false)]
    [InlineData(SensorStatus.Disconnected, false)]
    [InlineData(SensorStatus.Calibrating, false)]
    public void IsSensorReadyForThrottle_IsReadyOnly(SensorStatus status, bool ready)
    {
        ThrottleController.IsSensorReadyForThrottle(status).Should().Be(ready);
    }

    [Theory]
    [InlineData(SensorStatus.Unknown)]
    [InlineData(SensorStatus.Initializing)]
    [InlineData(SensorStatus.Reading)]
    [InlineData(SensorStatus.Error)]
    [InlineData(SensorStatus.Disconnected)]
    [InlineData(SensorStatus.Calibrating)]
    public async Task NotReadyThrustSensor_DoesNotOpenOnACachedShortfall(SensorStatus status)
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = status };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        await controller.ExecuteOnceAsync();

        // A trusted shortfall from closed steps open by the 0.001 rate limit.
        // The cached zero must not do that.
        controller.CurrentThrottle.Should().Be(0);
        actuator.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ReadyThrustSensor_ShortfallStillOpensTheThrottle()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.001, 1e-12);
    }

    [Fact]
    public async Task DisconnectedThrustSensor_StepsClosedInsteadOfHoldingACachedInBandReading()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);

        // Settle the PID on the target so a later cached reading would hold or
        // keep opening, rather than kicking the derivative term closed.
        thrust.Value = 1_000_000;
        await controller.ExecuteOnceAsync();
        await controller.ExecuteOnceAsync();
        var held = controller.CurrentThrottle;
        held.Should().BeGreaterThan(0);

        thrust.Status = SensorStatus.Disconnected;
        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(held - 0.001, 1e-9);
        actuator.LastPosition.Should().Be(controller.CurrentThrottle);
    }

    [Fact]
    public async Task NotReadyThrustSensor_RejectedClose_LeavesTheLastAcceptedPosition()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        actuator.RejectNext = true;
        thrust.Status = SensorStatus.Reading;
        thrust.Value = 1_000_000;

        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        var next = controller.ConstrainThrottleCommand(0);
        next.Should().BeApproximately(0.019, 1e-9);
    }

    [Fact]
    public async Task ActuatorCommandTimeout_LeavesTheLastAcceptedPositionAndRetries()
    {
        var actuator = new RecordingActuator { TimeoutCall = 1 };
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);

        await controller.ExecuteOnceAsync();
        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);

        var timedOut = () => controller.ExecuteOnceAsync();
        await timedOut.Should().NotThrowAsync();

        // The timed-out command was a step to 0.002. Storing it would make the
        // next accepted command 0.003. The retry starts from the last acceptance.
        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);
        await controller.ExecuteOnceAsync();
        controller.CurrentThrottle.Should().BeApproximately(0.002, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.002, 1e-12);
    }

    [Fact]
    public async Task CancelledActuatorCommand_PropagatesAndDoesNotMoveTheThrottle()
    {
        var actuator = new RecordingActuator();
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => controller.ExecuteOnceAsync(cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        controller.CurrentThrottle.Should().Be(0);
        actuator.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task ActuatorCommandTimeout_LeavesTheControlLoopRunning()
    {
        var actuator = new RecordingActuator { TimeoutCall = 0 };
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);

        await controller.StartAsync();
        var continued = await WaitUntilAsync(
            () => controller.IsRunning && controller.CurrentThrottle > 0 && actuator.CommandCount >= 2,
            TimeSpan.FromSeconds(5));
        continued.Should().BeTrue();
        controller.IsRunning.Should().BeTrue();

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.CurrentThrottle.Should().Be(0);
        controller.Dispose();
    }

    [Fact]
    public async Task ActuatorCommandThrows_LeavesTheLastAcceptedPositionAndRetries()
    {
        var actuator = new RecordingActuator { ThrowCall = 1 };
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);

        await controller.ExecuteOnceAsync();
        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);

        var thrown = () => controller.ExecuteOnceAsync();
        await thrown.Should().NotThrowAsync();

        // The thrown command was a step to 0.002 and did not land. Storing it
        // would make the next accepted command 0.003. The retry starts from the
        // last open command.
        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.001, 1e-12);
        await controller.ExecuteOnceAsync();
        controller.CurrentThrottle.Should().BeApproximately(0.002, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.002, 1e-12);
    }

    [Fact]
    public async Task ActuatorCommandThrows_WhileOpen_RetriesTheSameClose()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        actuator.ThrowCall = actuator.CommandCount;
        thrust.Value = 2_000_000;

        var thrown = () => controller.ExecuteOnceAsync();
        await thrown.Should().NotThrowAsync();

        // Overthrust commands one step closed. The throw did not land, so the
        // open position stays applied and the next pass retries that same step.
        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        actuator.LastPosition.Should().BeApproximately(0.02, 1e-9);

        actuator.ThrowCall = null;
        await controller.ExecuteOnceAsync();
        controller.CurrentThrottle.Should().BeApproximately(0.019, 1e-9);
        actuator.LastPosition.Should().BeApproximately(0.019, 1e-9);
    }

    [Fact]
    public async Task ActuatorCommandThrows_LeavesTheControlLoopRunning()
    {
        var actuator = new RecordingActuator { ThrowCall = 0 };
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);

        await controller.StartAsync();
        var continued = await WaitUntilAsync(
            () => controller.IsRunning && controller.CurrentThrottle > 0 && actuator.CommandCount >= 2,
            TimeSpan.FromSeconds(5));
        continued.Should().BeTrue();
        controller.IsRunning.Should().BeTrue();

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.CurrentThrottle.Should().Be(0);
        controller.Dispose();
    }

    [Fact]
    public async Task OpenLoop_NotReadyThrustSensor_StillFollowsTheCommand()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 1_000_000, Status = SensorStatus.Disconnected };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetThrottle(1);

        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.001, 1e-12);
    }

    [Fact]
    public async Task ClosedLoop_ThrustReadThrows_DoesNotOpenOnTheFirstPass()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor
        {
            Value = 0,
            Status = SensorStatus.Ready,
            ReadException = new InvalidOperationException("thrust sensor unavailable")
        };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        await controller.ExecuteOnceAsync();

        // A trusted shortfall from closed steps open by the 0.001 rate limit.
        // A thrown read must not do that, and it must not escape the pass.
        controller.CurrentThrottle.Should().Be(0);
        actuator.LastPosition.Should().Be(0);
        actuator.CommandCount.Should().Be(1);
    }

    [Fact]
    public async Task ClosedLoop_ThrustReadThrows_StepsClosedInsteadOfHolding()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        thrust.Value = 1_000_000;
        await controller.ExecuteOnceAsync();
        await controller.ExecuteOnceAsync();
        var held = controller.CurrentThrottle;
        held.Should().BeGreaterThan(0);

        thrust.ReadException = new InvalidOperationException("thrust sensor unavailable");
        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(held - 0.001, 1e-9);
        actuator.LastPosition.Should().Be(controller.CurrentThrottle);
    }

    [Fact]
    public async Task ClosedLoop_ThrustReadTimeout_StepsClosedAndTheNextPassStillRuns()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        thrust.ReadException = new TaskCanceledException("thrust read timed out");
        await controller.ExecuteOnceAsync();
        await controller.ExecuteOnceAsync();

        // A sensor timeout is not the loop stopping. Both passes step closed
        // from the last accepted position instead of leaving 0.02 applied.
        controller.CurrentThrottle.Should().BeApproximately(0.018, 1e-9);
        actuator.LastPosition.Should().BeApproximately(0.018, 1e-9);
    }

    [Fact]
    public async Task ClosedLoop_ThrustReadThrows_RejectedClose_LeavesTheLastAcceptedPosition()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        actuator.RejectNext = true;
        thrust.ReadException = new InvalidOperationException("thrust sensor unavailable");

        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        var next = controller.ConstrainThrottleCommand(0);
        next.Should().BeApproximately(0.019, 1e-9);
    }

    [Fact]
    public async Task ClosedLoop_ThrustReadThrows_CloseThrows_NextPassRetriesTheSameStep()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        actuator.ThrowNext = new InvalidOperationException("throttle actuator timed out");
        thrust.ReadException = new InvalidOperationException("thrust sensor unavailable");

        await controller.ExecuteOnceAsync();
        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);

        actuator.ThrowNext = null;
        await controller.ExecuteOnceAsync();

        // The thrown close is not the next origin. A stored 0.019 would step to 0.018.
        controller.CurrentThrottle.Should().BeApproximately(0.019, 1e-9);
        actuator.LastPosition.Should().BeApproximately(0.019, 1e-9);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task ClosedLoop_NonFiniteThrust_DoesNotOpenOnTheFirstPass(double thrustValue)
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = thrustValue, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        await controller.ExecuteOnceAsync();

        // A trusted shortfall from closed steps open by the 0.001 rate limit.
        // NaN compares as zero error, which used to do the same. A non-finite
        // sample is not a thrust, so the first command stays closed.
        controller.CurrentThrottle.Should().Be(0);
        actuator.LastPosition.Should().Be(0);
        actuator.CommandCount.Should().Be(1);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task ClosedLoop_NonFiniteThrust_StepsClosedInsteadOfHolding(double thrustValue)
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        for (var i = 0; i < 20; i++)
            await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.02, 1e-9);
        thrust.Value = 1_000_000;
        await controller.ExecuteOnceAsync();
        await controller.ExecuteOnceAsync();
        var held = controller.CurrentThrottle;
        held.Should().BeGreaterThan(0);

        thrust.Value = thrustValue;
        await controller.ExecuteOnceAsync();
        await controller.ExecuteOnceAsync();

        // Zero error would hold the last command. Both passes step closed.
        controller.CurrentThrottle.Should().BeApproximately(held - 0.002, 1e-9);
        actuator.LastPosition.Should().Be(controller.CurrentThrottle);
    }

    [Fact]
    public async Task OpenLoop_NonFiniteThrust_StillFollowsTheCommand()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = double.NaN, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetThrottle(1);

        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.001, 1e-12);
    }

    [Fact]
    public async Task OpenLoop_ThrustReadThrows_StillFollowsTheCommand()
    {
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor
        {
            Value = 1_000_000,
            Status = SensorStatus.Ready,
            ReadException = new InvalidOperationException("thrust sensor unavailable")
        };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetThrottle(1);

        await controller.ExecuteOnceAsync();

        controller.CurrentThrottle.Should().BeApproximately(0.001, 1e-12);
        actuator.LastPosition.Should().BeApproximately(0.001, 1e-12);
    }

    [Fact]
    public async Task CancelledPass_DoesNotTreatTheStopAsAThrustFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var actuator = new RecordingActuator();
        var thrust = new ScriptedSensor { Value = 0, Status = SensorStatus.Ready };
        var controller = new ThrottleController(actuator, thrust, new StubSensor());
        controller.SetTargetThrust(1_000_000);

        var act = async () => await controller.ExecuteOnceAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        actuator.CommandCount.Should().Be(0);
        controller.CurrentThrottle.Should().Be(0);
    }

    [Fact]
    public async Task Restart_AfterStop_CommandsTheThrottleAgain()
    {
        var actuator = new RecordingActuator();
        var controller = new ThrottleController(actuator, new StubSensor(), new StubSensor());
        controller.SetThrottle(1);

        await controller.StartAsync();
        var opened = await WaitUntilAsync(
            () => controller.CurrentThrottle > 0,
            TimeSpan.FromSeconds(5));
        opened.Should().BeTrue();

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.CurrentThrottle.Should().Be(0);
        actuator.LastPosition.Should().Be(0);

        await controller.StartAsync();
        var reopened = await WaitUntilAsync(
            () => controller.CurrentThrottle > 0,
            TimeSpan.FromSeconds(5));
        reopened.Should().BeTrue();
        controller.IsRunning.Should().BeTrue();
        actuator.LastPosition.Should().Be(controller.CurrentThrottle);

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        controller.CurrentThrottle.Should().Be(0);
        controller.Dispose();
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
                return true;

            await Task.Delay(10);
        }

        return predicate();
    }

    private static ThrottleController CreateController()
    {
        return new ThrottleController(new StubActuator(), new StubSensor(), new StubSensor());
    }

    private sealed class StubActuator : IActuator
    {
        public string ActuatorId => "throttle";
        public string Name => "throttle";
        public ActuatorType Type => ActuatorType.Throttle;
        public ActuatorStatus Status => ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.1;
        public double MaxRateOfChange => 1;
        public bool IsEnabled => true;

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            return Task.FromResult(true);
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0d);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingActuator : IActuator
    {
        private readonly object _gate = new();
        private readonly List<double> _positions = new();

        public double LastPosition
        {
            get
            {
                lock (_gate)
                {
                    return _positions[^1];
                }
            }
        }

        public string ActuatorId => "throttle";
        public string Name => "throttle";
        public ActuatorType Type => ActuatorType.Throttle;
        public ActuatorStatus Status => ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.01;
        public double MaxRateOfChange => 1;
        public bool IsEnabled => true;

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public bool RejectNext { get; set; }
        public Exception? ThrowNext { get; set; }

        /// <summary>
        /// Zero-based command index that throws <see cref="TaskCanceledException"/>
        /// without the caller's token being cancelled.
        /// </summary>
        public int? TimeoutCall { get; set; }

        /// <summary>
        /// Zero-based command index that throws <see cref="InvalidOperationException"/>.
        /// The position is not recorded, matching a command that did not land.
        /// </summary>
        public int? ThrowCall { get; set; }

        public int CommandCount
        {
            get
            {
                lock (_gate)
                    return _calls;
            }
        }

        private int _calls;

        public Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool timeout;
            bool fault;
            lock (_gate)
            {
                var index = _calls++;
                timeout = TimeoutCall == index;
                fault = ThrowCall == index;
                if (!timeout && !fault)
                    _positions.Add(position);
            }

            if (timeout)
                throw new TaskCanceledException("actuator timeout");

            if (fault)
                throw new InvalidOperationException("actuator command failed");

            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            if (ThrowNext != null)
            {
                var error = ThrowNext;
                ThrowNext = null;
                return Task.FromException<bool>(error);
            }

            if (RejectNext)
            {
                RejectNext = false;
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0d);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class ScriptedSensor : ISensor<double>
    {
        public double Value { get; set; }
        public SensorStatus Status { get; set; } = SensorStatus.Ready;
        public Exception? ReadException { get; set; }
        public string SensorId => "thrust";
        public string Name => "thrust";
        public SensorType Type => SensorType.Other;
        public double MinValue => 0;
        public double MaxValue => double.MaxValue;
        public int MaxFrequencyHz => 100;
        public DateTime LastReadingTime => DateTime.UtcNow;

        public event EventHandler<SensorReadingChangedEventArgs<double>>? ReadingChanged;

        public Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromException<double>(new OperationCanceledException(cancellationToken));

            if (ReadException != null)
                return Task.FromException<double>(ReadException);

            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(Value);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubSensor : ISensor<double>
    {
        public string SensorId => "sensor";
        public string Name => "sensor";
        public SensorType Type => SensorType.Pressure;
        public SensorStatus Status => SensorStatus.Ready;
        public double MinValue => 0;
        public double MaxValue => 1;
        public int MaxFrequencyHz => 100;
        public DateTime LastReadingTime => DateTime.UtcNow;

        public event EventHandler<SensorReadingChangedEventArgs<double>>? ReadingChanged;

        public Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(0d);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
