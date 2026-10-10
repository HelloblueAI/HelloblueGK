using HB_NLP_Research_Lab.Core.Control;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HelloblueGK.Tests.Unit.Core;

public class StartupSequenceControllerTests
{
    private const double StandardAtmospherePascals = 101_325;

    [Fact]
    public void AmbientBand_AcceptsSeaLevelPressure_AndRejectsNonFinite()
    {
        StartupSequenceController.IsAmbientPressure(StandardAtmospherePascals).Should().BeTrue();
        StartupSequenceController.IsAmbientPressure(0).Should().BeTrue();
        StartupSequenceController.IsAmbientPressure(StartupSequenceController.MaxAmbientPressurePascals).Should().BeTrue();

        // The previous ceiling was 100 kPa, which is below standard atmosphere.
        StartupSequenceController.IsAmbientPressure(100_000).Should().BeTrue();
        StartupSequenceController.IsAmbientPressure(StartupSequenceController.MaxAmbientPressurePascals + 1).Should().BeFalse();
        StartupSequenceController.IsAmbientPressure(-1).Should().BeFalse();
        StartupSequenceController.IsAmbientPressure(double.NaN).Should().BeFalse();
        StartupSequenceController.IsAmbientPressure(double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void AmbientTemperature_RejectsNonFinite()
    {
        StartupSequenceController.IsAmbientTemperature(293.15).Should().BeTrue();
        StartupSequenceController.IsAmbientTemperature(199).Should().BeFalse();
        StartupSequenceController.IsAmbientTemperature(401).Should().BeFalse();
        StartupSequenceController.IsAmbientTemperature(double.NaN).Should().BeFalse();
    }

    [Fact]
    public void Combustion_RequiresAPressureRiseOutOfTheAmbientBand()
    {
        StartupSequenceController.IsCombustionEstablished(StandardAtmospherePascals, 1500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(50_000, 1500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(120_000, 999).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(120_000, double.NaN).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(double.NaN, 1500).Should().BeFalse();

        StartupSequenceController.IsCombustionEstablished(120_000, 1000).Should().BeTrue();
        StartupSequenceController.IsCombustionEstablished(
            StartupSequenceController.MaximumChamberPressurePascals,
            StartupSequenceController.MaximumChamberTemperatureKelvin).Should().BeTrue();
    }

    [Fact]
    public void Combustion_RejectsAPeggedChamberSensor()
    {
        StartupSequenceController.IsCombustionEstablished(
            StartupSequenceController.MaximumChamberPressurePascals + 1,
            1_500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(double.MaxValue, 1_500).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(
            200_000,
            StartupSequenceController.MaximumChamberTemperatureKelvin + 1).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(200_000, double.MaxValue).Should().BeFalse();
        StartupSequenceController.IsCombustionEstablished(200_000, double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void OperatingPressure_IsAboveTheAmbientBand()
    {
        StartupSequenceController.IsOperatingPressure(100_000).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MinOperatingPressurePascals - 1).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MinOperatingPressurePascals).Should().BeTrue();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MaximumChamberPressurePascals)
            .Should().BeTrue();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MaximumChamberPressurePascals + 1)
            .Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.MaxValue).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.NaN).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.PositiveInfinity).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(-1).Should().BeFalse();
    }

    [Fact]
    public void PropellantFlow_RejectsAMissingReading()
    {
        StartupSequenceController.HasMinimumPropellantFlow(0.01).Should().BeTrue();
        StartupSequenceController.HasMinimumPropellantFlow(0.009).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.NaN).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.NegativeInfinity).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void PropellantFlow_RejectsAPeggedSensor()
    {
        StartupSequenceController.IsPropellantFlowInBand(0.01, StartupSequenceController.MaximumFuelFlowKgPerSecond)
            .Should().BeTrue();
        StartupSequenceController.IsPropellantFlowInBand(
            StartupSequenceController.MaximumFuelFlowKgPerSecond,
            StartupSequenceController.MaximumFuelFlowKgPerSecond).Should().BeTrue();
        StartupSequenceController.IsPropellantFlowInBand(
            StartupSequenceController.MaximumOxidizerFlowKgPerSecond,
            StartupSequenceController.MaximumOxidizerFlowKgPerSecond).Should().BeTrue();

        StartupSequenceController.IsPropellantFlowInBand(
            StartupSequenceController.MaximumFuelFlowKgPerSecond + 1,
            StartupSequenceController.MaximumFuelFlowKgPerSecond).Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(
            double.MaxValue,
            StartupSequenceController.MaximumFuelFlowKgPerSecond).Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(double.NaN, StartupSequenceController.MaximumFuelFlowKgPerSecond)
            .Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(1, double.NaN).Should().BeFalse();
        StartupSequenceController.IsPropellantFlowInBand(1, double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public async Task PreStartup_NonFinitePressure_ClosesValvesOnThisPass()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            new StubSensor { Value = double.NaN });

        controller.MoveToState(StartupState.PreStartupChecks);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.HasError.Should().BeTrue();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_RejectedValveCommand_DoesNotContinueWhenFlowLooksEstablished()
    {
        var fuel = new RecordingActuator { RejectWhen = static _ => true };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
        oxidizer.Positions.Should().OnlyContain(position => position == 0);
        igniter.Positions.Should().OnlyContain(position => position == 0);
    }

    [Fact]
    public async Task Ignition_RejectedIgniterShutdown_DoesNotLeaveTheSequenceRunning()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator { RejectWhen = static position => position == 0 };
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        controller.CurrentState.Should().NotBe(StartupState.Running);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.Positions.Should().Contain(1);
        igniter.Positions.Should().Contain(0);
    }

    [Fact]
    public async Task FuelFlow_PeggedSensor_DoesNotContinueAndClosesTheValve()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = double.MaxValue });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.OxidizerFlowInitiation);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task Combustion_PeggedTemperature_DoesNotContinueAndClosesTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = 200_000 },
            temperature: new StubSensor { Value = double.MaxValue });

        controller.MoveToState(StartupState.CombustionVerification);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.CurrentState.Should().NotBe(StartupState.ThrottleUp);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_PeggedPressure_DoesNotDeclareRunningAndClosesTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = double.MaxValue });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Error);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task ThrottleUp_PressureStillBelowOperating_KeepsTheValvesAsTheyWere()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            pressure: new StubSensor { Value = 120_000 });

        controller.MoveToState(StartupState.ThrottleUp);
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.ThrottleUp);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task Stop_FromRunning_ClosesThePropellantValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Running);
        await controller.StartAsync();
        await controller.StopAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
        controller.Dispose();
    }

    [Fact]
    public async Task Stop_FromIdle_DoesNotCommandTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        await controller.StartAsync();
        await controller.StopAsync();

        controller.CurrentState.Should().Be(StartupState.Idle);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
        controller.Dispose();
    }

    [Fact]
    public void AbortStartup_FromRunning_ClosesThePropellantValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Running);
        controller.AbortStartup();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);
    }

    [Fact]
    public async Task FuelFlow_AbortDuringOpen_DoesNotResumeOrLeaveTheValveOpen()
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOpen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartupSequenceController? controller = null;
        var fuel = new RecordingActuator
        {
            OnCommanding = async position =>
            {
                if (position <= 0)
                    return;

                opened.TrySetResult();
                await releaseOpen.Task;
            }
        };
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        controller = CreateController(
            fuel,
            oxidizer,
            igniter,
            fuelFlow: new StubSensor { Value = 1 });

        controller.MoveToState(StartupState.FuelFlowInitiation);
        var pass = controller.ExecuteOnceAsync();
        await opened.Task;
        controller.AbortStartup();
        releaseOpen.TrySetResult();
        await pass;

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.IsStartupComplete.Should().BeFalse();
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
        igniter.LastPosition.Should().Be(0);

        var commandsAtAbort = fuel.CommandCount;
        await controller.ExecuteOnceAsync();

        controller.CurrentState.Should().Be(StartupState.Aborted);
        fuel.Positions.Skip(commandsAtAbort).Should().OnlyContain(position => position == 0);
        oxidizer.Positions.SkipWhile(position => position == 0).Should().BeEmpty();
    }

    [Fact]
    public async Task Ignition_AbortDuringSpark_DoesNotLeaveTheIgniterOnOrContinue()
    {
        var sparked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSpark = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StartupSequenceController? controller = null;
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator
        {
            OnCommanding = async position =>
            {
                if (position <= 0)
                    return;

                sparked.TrySetResult();
                await releaseSpark.Task;
            }
        };
        controller = CreateController(fuel, oxidizer, igniter);

        controller.MoveToState(StartupState.Ignition);
        var pass = controller.ExecuteOnceAsync();
        await sparked.Task;
        controller.AbortStartup();
        releaseSpark.TrySetResult();
        await pass;

        controller.CurrentState.Should().Be(StartupState.Aborted);
        controller.CurrentState.Should().NotBe(StartupState.CombustionVerification);
        igniter.LastPosition.Should().Be(0);
        fuel.LastPosition.Should().Be(0);
        oxidizer.LastPosition.Should().Be(0);
    }

    [Fact]
    public void AbortStartup_FromIdle_DoesNotCommandTheValves()
    {
        var fuel = new RecordingActuator();
        var oxidizer = new RecordingActuator();
        var igniter = new RecordingActuator();
        var controller = CreateController(fuel, oxidizer, igniter);

        controller.AbortStartup();

        controller.CurrentState.Should().Be(StartupState.Idle);
        fuel.CommandCount.Should().Be(0);
        oxidizer.CommandCount.Should().Be(0);
        igniter.CommandCount.Should().Be(0);
    }

    private static StartupSequenceController CreateController(
        IActuator fuel,
        IActuator oxidizer,
        IActuator igniter,
        ISensor<double>? pressure = null,
        ISensor<double>? temperature = null,
        ISensor<double>? fuelFlow = null,
        ISensor<double>? oxidizerFlow = null)
    {
        return new StartupSequenceController(
            fuel,
            oxidizer,
            igniter,
            pressure ?? new StubSensor(),
            temperature ?? new StubSensor(),
            fuelFlow ?? new StubSensor(),
            oxidizerFlow ?? new StubSensor());
    }

    private sealed class RecordingActuator : IActuator
    {
        public string ActuatorId => "actuator";
        public string Name => "actuator";
        public ActuatorType Type => ActuatorType.Valve;
        public ActuatorStatus Status => ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.1;
        public double MaxRateOfChange => 1;
        public bool IsEnabled => true;
        public int CommandCount { get; private set; }
        public double LastPosition { get; private set; } = double.NaN;
        public List<double> Positions { get; } = new();
        public Func<double, Task>? OnCommanding { get; init; }

        /// <summary>
        /// When this returns true, the command is recorded and rejected.
        /// </summary>
        public Predicate<double>? RejectWhen { get; init; }

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public async Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            if (OnCommanding != null)
                await OnCommanding(position);

            CommandCount++;
            LastPosition = position;
            Positions.Add(position);
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            return RejectWhen == null || !RejectWhen(position);
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LastPosition);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubSensor : ISensor<double>
    {
        public string SensorId => "sensor";
        public string Name => "sensor";
        public SensorType Type => SensorType.Pressure;
        public SensorStatus Status => SensorStatus.Ready;
        public double MinValue => 0;
        public double MaxValue => 1;
        public int MaxFrequencyHz => 10;
        public DateTime LastReadingTime => DateTime.UtcNow;

        public event EventHandler<SensorReadingChangedEventArgs<double>>? ReadingChanged;

        public double Value { get; set; }

        public Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(Value);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
