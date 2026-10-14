using HB_NLP_Research_Lab.Core.Hardware;
using HB_NLP_Research_Lab.Core.Safety;

namespace HelloblueGK.Tests.Unit.Core;

public class HardwareSafetyMonitorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(35_000_000, false)]
    [InlineData(-1, true)]
    [InlineData(35_000_001, true)]
    [InlineData(double.NaN, true)]
    [InlineData(double.PositiveInfinity, true)]
    [InlineData(double.NegativeInfinity, true)]
    public void ReadingViolatesLimit_RejectsOutOfRangeAndNonFinite(double value, bool violates)
    {
        HardwareSafetyMonitor.ReadingViolatesLimit(value, 0, 35_000_000).Should().Be(violates);
    }

    [Fact]
    public void ReadingViolatesLimit_RejectsAnUnusableBound()
    {
        HardwareSafetyMonitor.ReadingViolatesLimit(1, double.NaN, 10).Should().BeTrue();
        HardwareSafetyMonitor.ReadingViolatesLimit(1, 0, double.PositiveInfinity).Should().BeTrue();
        HardwareSafetyMonitor.ReadingViolatesLimit(1, 10, 0).Should().BeTrue();
    }

    [Fact]
    public void SetSafetyLimit_RejectsANonFiniteCeiling()
    {
        var monitor = new HardwareSafetyMonitor(new List<ISensor<double>>());

        var act = () => monitor.SetSafetyLimit("ChamberPressure", 0, double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task NaNChamberPressure_TripsEmergencyShutdown()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator);

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.LastPosition.Should().Be(1);
        actuator.CommandCount.Should().Be(1);
    }

    [Fact]
    public async Task AcceptedShutdown_IsNotCommandedAgain()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator);

        await monitor.CheckSensorsOnceAsync();
        await monitor.CheckSensorsOnceAsync();

        actuator.CommandCount.Should().Be(1);
        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedShutdownCommand_RetriesOnTheNextPassEvenIfTheReadingRecovers(bool throwOnFailure)
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: 1, throwOnFailure: throwOnFailure);
        var sensor = new ScriptedSensor("ChamberPressure", double.NaN);
        var monitor = new HardwareSafetyMonitor(new List<ISensor<double>> { sensor }, actuator);
        var shutdowns = 0;
        var commandFailures = 0;
        monitor.EmergencyShutdownTriggered += (_, _) => shutdowns++;
        monitor.EmergencyShutdownCommandFailed += (_, args) =>
        {
            commandFailures++;
            args.ConsecutiveFailureCount.Should().Be(1);
        };

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeTrue();
        shutdowns.Should().Be(0);
        commandFailures.Should().Be(1);
        actuator.CommandCount.Should().Be(1);

        sensor.Value = 30_000_000;
        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        shutdowns.Should().Be(1);
        commandFailures.Should().Be(1);
        actuator.CommandCount.Should().Be(2);
        actuator.LastPosition.Should().Be(1);
    }

    [Fact]
    public async Task RejectedShutdownCommand_IsVisibleAsPendingAndRaisesFailureEvent()
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: 1);
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator);
        EmergencyShutdownCommandFailedEventArgs? commandFailure = null;
        monitor.EmergencyShutdownCommandFailed += (_, args) => commandFailure = args;

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeTrue();
        commandFailure.Should().NotBeNull();
        commandFailure!.Reason.Should().Be("Critical safety violation: ChamberPressure");
        commandFailure.FailureMessage.Should().Contain("rejected");
        commandFailure.ConsecutiveFailureCount.Should().Be(1);
    }

    [Fact]
    public async Task ContinuouslyRejectedShutdown_EscalatesOnceAndKeepsRetrying()
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: int.MaxValue);
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator,
            emergencyShutdownFailureEscalationThreshold: 3,
            emergencyShutdownFailureLogInterval: 2);
        var commandFailures = new List<EmergencyShutdownCommandFailedEventArgs>();
        var escalations = new List<EmergencyShutdownEscalationEventArgs>();
        var shutdowns = 0;
        monitor.EmergencyShutdownCommandFailed += (_, args) => commandFailures.Add(args);
        monitor.EmergencyShutdownEscalated += (_, args) => escalations.Add(args);
        monitor.EmergencyShutdownTriggered += (_, _) => shutdowns++;

        for (var i = 0; i < 5; i++)
        {
            await monitor.CheckSensorsOnceAsync();
        }

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeTrue();
        actuator.CommandCount.Should().Be(5);
        shutdowns.Should().Be(0);
        commandFailures.Select(args => args.ConsecutiveFailureCount).Should().Equal(1, 2, 3, 4, 5);
        escalations.Should().ContainSingle();
        escalations[0].ConsecutiveFailureCount.Should().Be(3);
        escalations[0].FailureThreshold.Should().Be(3);

        await monitor.CheckSensorsOnceAsync();

        actuator.CommandCount.Should().Be(6);
        commandFailures.Select(args => args.ConsecutiveFailureCount).Should().Equal(1, 2, 3, 4, 5, 6);
        escalations.Should().ContainSingle();
    }

    [Fact]
    public async Task ResetAfterAcceptedShutdown_DoesNotCommandAgainForAnInRangeReading()
    {
        var actuator = new RecordingActuator();
        var sensor = new ScriptedSensor("ChamberPressure", double.NaN);
        var monitor = new HardwareSafetyMonitor(new List<ISensor<double>> { sensor }, actuator);

        await monitor.CheckSensorsOnceAsync();
        monitor.ResetEmergencyShutdown();
        sensor.Value = 30_000_000;
        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        actuator.CommandCount.Should().Be(1);
    }

    [Fact]
    public async Task ResetWhileShutdownIsPending_StopsTheRetry()
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: 5);
        var sensor = new ScriptedSensor("ChamberPressure", double.PositiveInfinity);
        var monitor = new HardwareSafetyMonitor(new List<ISensor<double>> { sensor }, actuator);

        await monitor.CheckSensorsOnceAsync();
        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeTrue();

        monitor.ResetEmergencyShutdown();
        sensor.Value = 1_000_000;
        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.CommandCount.Should().Be(1);
    }

    [Fact]
    public async Task ThrowingViolationSubscriber_StillCommandsEmergencyShutdown()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator);
        var notifications = 0;
        monitor.SafetyViolationDetected += (_, _) =>
            throw new InvalidOperationException("subscriber failure should be isolated");
        monitor.SafetyViolationDetected += (_, _) => notifications++;

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.LastPosition.Should().Be(1);
        actuator.CommandCount.Should().Be(1);
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task ThrowingShutdownFailureSubscriber_StillRetriesOnTheNextPass()
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: 1);
        var sensor = new ScriptedSensor("ChamberPressure", double.NaN);
        var monitor = new HardwareSafetyMonitor(new List<ISensor<double>> { sensor }, actuator);
        var failureNotifications = 0;
        monitor.EmergencyShutdownCommandFailed += (_, _) =>
        {
            failureNotifications++;
            throw new InvalidOperationException("subscriber failure should be isolated");
        };
        monitor.EmergencyShutdownCommandFailed += (_, _) => failureNotifications++;

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeTrue();
        actuator.CommandCount.Should().Be(1);
        failureNotifications.Should().Be(2);

        sensor.Value = 1_000_000;
        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.CommandCount.Should().Be(2);
        actuator.LastPosition.Should().Be(1);
    }

    [Fact]
    public async Task ThrowingShutdownSubscriber_LeavesTheAcceptedShutdownInPlace()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.PositiveInfinity) },
            actuator);
        monitor.EmergencyShutdownTriggered += (_, _) =>
            throw new InvalidOperationException("subscriber failure should be isolated");

        await monitor.CheckSensorsOnceAsync();
        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        actuator.CommandCount.Should().Be(1);
        actuator.LastPosition.Should().Be(1);
    }

    [Fact]
    public async Task ThrowingEscalationSubscriber_StillKeepsRetryingShutdown()
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: int.MaxValue);
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator,
            emergencyShutdownFailureEscalationThreshold: 3);
        var escalations = 0;
        monitor.EmergencyShutdownEscalated += (_, _) =>
        {
            escalations++;
            throw new InvalidOperationException("subscriber failure should be isolated");
        };

        for (var i = 0; i < 4; i++)
        {
            await monitor.CheckSensorsOnceAsync();
        }

        monitor.IsEmergencyShutdownPending.Should().BeTrue();
        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        actuator.CommandCount.Should().Be(4);
        escalations.Should().Be(1);
    }

    [Fact]
    public async Task InRangeChamberPressure_DoesNotTrip()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", 30_000_000) },
            actuator);

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task PositiveInfinityFuelFlow_TripsEmergencyShutdown()
    {
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("FuelFlow", double.PositiveInfinity) });

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
    }

    [Fact]
    public async Task CancelledSensorRead_DoesNotTripEmergencyShutdown()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>>
            {
                new ScriptedSensor("ChamberPressure", 1_000_000) { ThrowOnCancellation = true }
            },
            actuator);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => monitor.CheckSensorsOnceAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        monitor.IsEmergencyShutdownActive.Should().BeFalse();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task SensorReadTimeout_TripsEmergencyShutdown()
    {
        var actuator = new RecordingActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>>
            {
                new ScriptedSensor("ChamberPressure", 1_000_000) { ThrowTimeout = true }
            },
            actuator);

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
        monitor.IsEmergencyShutdownPending.Should().BeFalse();
        actuator.CommandCount.Should().Be(1);
        actuator.LastPosition.Should().Be(1);
    }

    [Fact]
    public async Task StopDuringInRangeRead_DoesNotTripEmergencyShutdown()
    {
        var actuator = new RecordingActuator();
        var sensor = new BlockingSensor("ChamberPressure", 1_000_000);
        var monitor = new HardwareSafetyMonitor(new List<ISensor<double>> { sensor }, actuator);

        try
        {
            await monitor.StartAsync();
            await sensor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await monitor.StopAsync();

            monitor.IsRunning.Should().BeFalse();
            monitor.IsEmergencyShutdownActive.Should().BeFalse();
            monitor.IsEmergencyShutdownPending.Should().BeFalse();
            actuator.CommandCount.Should().Be(0);

            sensor.Block = false;
            await monitor.CheckSensorsOnceAsync();

            monitor.IsEmergencyShutdownActive.Should().BeFalse();
            monitor.IsEmergencyShutdownPending.Should().BeFalse();
            actuator.CommandCount.Should().Be(0);
        }
        finally
        {
            monitor.Dispose();
        }
    }

    [Fact]
    public async Task StopDuringInFlightShutdown_CompletesTheCommandStopCannotCancel()
    {
        var actuator = new CancelThenAcceptActuator();
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator);
        var shutdowns = 0;
        var commandFailures = 0;
        monitor.EmergencyShutdownTriggered += (_, _) => shutdowns++;
        monitor.EmergencyShutdownCommandFailed += (_, _) => commandFailures++;

        try
        {
            await monitor.StartAsync();
            await actuator.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await monitor.StopAsync();

            monitor.IsRunning.Should().BeFalse();
            monitor.IsEmergencyShutdownActive.Should().BeTrue();
            monitor.IsEmergencyShutdownPending.Should().BeFalse();
            actuator.CommandCount.Should().Be(2);
            actuator.LastPosition.Should().Be(1);
            actuator.LastTokenCanBeCanceled.Should().BeFalse();
            shutdowns.Should().Be(1);
            commandFailures.Should().Be(0);
        }
        finally
        {
            monitor.Dispose();
        }
    }

    [Fact]
    public async Task StopWhileShutdownStaysRejected_ThrowsAndLeavesTheTripPending()
    {
        var actuator = new RecordingActuator(failuresBeforeAccept: int.MaxValue);
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("ChamberPressure", double.NaN) },
            actuator);

        try
        {
            await monitor.StartAsync();
            (await WaitUntilAsync(() => actuator.CommandCount > 0, TimeSpan.FromSeconds(5))).Should().BeTrue();

            var stop = async () => await monitor.StopAsync();

            await stop.Should().ThrowAsync<InvalidOperationException>();
            monitor.IsRunning.Should().BeFalse();
            monitor.IsEmergencyShutdownActive.Should().BeFalse();
            monitor.IsEmergencyShutdownPending.Should().BeTrue();
        }
        finally
        {
            monitor.Dispose();
        }
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

    private sealed class ScriptedSensor : ISensor<double>
    {
        public ScriptedSensor(string name, double value)
        {
            Name = name;
            Value = value;
        }

        public double Value { get; set; }
        public bool ThrowOnCancellation { get; set; }
        public bool ThrowTimeout { get; set; }

        public string SensorId => Name;
        public string Name { get; }
        public SensorType Type => SensorType.Pressure;
        public SensorStatus Status => SensorStatus.Ready;
        public double MinValue => 0;
        public double MaxValue => 1;
        public int MaxFrequencyHz => 10;
        public DateTime LastReadingTime => DateTime.UtcNow;

        public event EventHandler<SensorReadingChangedEventArgs<double>>? ReadingChanged;

        public Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnCancellation)
                cancellationToken.ThrowIfCancellationRequested();

            if (ThrowTimeout)
                throw new TaskCanceledException("sensor timeout");

            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(Value);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingActuator : IActuator
    {
        private readonly int _failuresBeforeAccept;
        private readonly bool _throwOnFailure;

        public RecordingActuator(int failuresBeforeAccept = 0, bool throwOnFailure = false)
        {
            _failuresBeforeAccept = failuresBeforeAccept;
            _throwOnFailure = throwOnFailure;
        }

        public string ActuatorId => "shutdown";
        public string Name => "shutdown";
        public ActuatorType Type => ActuatorType.Valve;
        public ActuatorStatus Status => ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.1;
        public double MaxRateOfChange => 1;
        public bool IsEnabled => true;
        public int CommandCount { get; private set; }
        public double LastPosition { get; private set; } = double.NaN;

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            CommandCount++;
            LastPosition = position;
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            if (CommandCount <= _failuresBeforeAccept)
            {
                if (_throwOnFailure)
                    throw new InvalidOperationException("actuator not ready");

                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LastPosition);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class BlockingSensor : ISensor<double>
    {
        public BlockingSensor(string name, double value)
        {
            Name = name;
            Value = value;
        }

        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Block { get; set; } = true;
        public double Value { get; set; }
        public string SensorId => Name;
        public string Name { get; }
        public SensorType Type => SensorType.Pressure;
        public SensorStatus Status => SensorStatus.Ready;
        public double MinValue => 0;
        public double MaxValue => 1;
        public int MaxFrequencyHz => 10;
        public DateTime LastReadingTime => DateTime.UtcNow;

        public event EventHandler<SensorReadingChangedEventArgs<double>>? ReadingChanged;

        public async Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            if (Block)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Value;
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    /// <summary>
    /// The first command waits until its token is cancelled, which is what a stop does
    /// to the in-flight shutdown. The next command is accepted.
    /// </summary>
    private sealed class CancelThenAcceptActuator : IActuator
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ActuatorId => "shutdown";
        public string Name => "shutdown";
        public ActuatorType Type => ActuatorType.Valve;
        public ActuatorStatus Status => ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.1;
        public double MaxRateOfChange => 1;
        public bool IsEnabled => true;
        public int CommandCount { get; private set; }
        public double LastPosition { get; private set; } = double.NaN;
        public bool LastTokenCanBeCanceled { get; private set; } = true;

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public async Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            CommandCount++;
            LastPosition = position;
            LastTokenCanBeCanceled = cancellationToken.CanBeCanceled;
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            if (CommandCount == 1)
            {
                Entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return true;
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LastPosition);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
