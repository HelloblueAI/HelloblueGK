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
        actuator.LastPosition.Should().Be(1);
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
        actuator.CommandCount.Should().Be(0);
    }

    [Fact]
    public async Task PositiveInfinityFuelFlow_TripsEmergencyShutdown()
    {
        var monitor = new HardwareSafetyMonitor(
            new List<ISensor<double>> { new ScriptedSensor("FuelFlow", double.PositiveInfinity) });

        await monitor.CheckSensorsOnceAsync();

        monitor.IsEmergencyShutdownActive.Should().BeTrue();
    }

    private sealed class ScriptedSensor : ISensor<double>
    {
        private readonly double _value;

        public ScriptedSensor(string name, double value)
        {
            Name = name;
            _value = value;
        }

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
            ReadingChanged?.Invoke(this, new SensorReadingChangedEventArgs<double>());
            return Task.FromResult(_value);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingActuator : IActuator
    {
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
            return Task.FromResult(true);
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(LastPosition);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
