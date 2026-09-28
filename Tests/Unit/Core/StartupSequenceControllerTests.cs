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
    }

    [Fact]
    public void OperatingPressure_IsAboveTheAmbientBand()
    {
        StartupSequenceController.IsOperatingPressure(100_000).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MinOperatingPressurePascals - 1).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(StartupSequenceController.MinOperatingPressurePascals).Should().BeTrue();
        StartupSequenceController.IsOperatingPressure(double.NaN).Should().BeFalse();
        StartupSequenceController.IsOperatingPressure(double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public void PropellantFlow_RejectsAMissingReading()
    {
        StartupSequenceController.HasMinimumPropellantFlow(0.01).Should().BeTrue();
        StartupSequenceController.HasMinimumPropellantFlow(0.009).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.NaN).Should().BeFalse();
        StartupSequenceController.HasMinimumPropellantFlow(double.NegativeInfinity).Should().BeFalse();
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
        IActuator igniter)
    {
        var pressure = new StubSensor();
        var temperature = new StubSensor();
        var fuelFlow = new StubSensor();
        var oxidizerFlow = new StubSensor();
        return new StartupSequenceController(
            fuel,
            oxidizer,
            igniter,
            pressure,
            temperature,
            fuelFlow,
            oxidizerFlow);
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

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            CommandCount++;
            LastPosition = position;
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            return Task.FromResult(true);
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

        public Task<double> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(0d);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
