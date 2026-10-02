using HB_NLP_Research_Lab.Core.Control;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HelloblueGK.Tests.Unit.Core;

public class ModelPredictiveControllerTests
{
    [Fact]
    public void ConstrainCommand_NonFiniteCommand_HoldsTheLastFinitePosition()
    {
        ModelPredictiveController.ConstrainCommand(double.NaN, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().Be(0.4);
        ModelPredictiveController.ConstrainCommand(double.PositiveInfinity, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().Be(0.4);
        ModelPredictiveController.ConstrainCommand(double.NegativeInfinity, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().Be(0.4);
    }

    [Fact]
    public void ConstrainCommand_NonFinitePrevious_DoesNotSkipTheRateLimit()
    {
        // A failed previous command is the minimum. The next finite command may
        // only step by the rate window, not jump to the requested position.
        ModelPredictiveController.ConstrainCommand(0.8, double.NaN, true, 0, 1, -0.1, 0.1)
            .Should().BeApproximately(0.1, 1e-12);
        ModelPredictiveController.ConstrainCommand(0.05, double.NaN, true, 0, 1, -0.1, 0.1)
            .Should().BeApproximately(0.05, 1e-12);
        ModelPredictiveController.ConstrainCommand(double.NaN, double.NaN, true, 0, 1, -0.1, 0.1)
            .Should().Be(0);
    }

    [Fact]
    public void ConstrainCommand_NoHistory_NonFiniteCommand_CommandsTheMinimum()
    {
        ModelPredictiveController.ConstrainCommand(double.NaN, 0, false, 0.2, 1, -0.1, 0.1)
            .Should().Be(0.2);
        ModelPredictiveController.ConstrainCommand(double.PositiveInfinity, 0, false, 0, 1, -0.1, 0.1)
            .Should().Be(0);
    }

    [Fact]
    public void ConstrainCommand_ClampsThenRateLimitsAFiniteCommand()
    {
        // Inclusive rate window: a step equal to the limit is kept.
        ModelPredictiveController.ConstrainCommand(0.5, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().BeApproximately(0.5, 1e-12);
        ModelPredictiveController.ConstrainCommand(0.3, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().BeApproximately(0.3, 1e-12);

        ModelPredictiveController.ConstrainCommand(0.6, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().BeApproximately(0.5, 1e-12);
        ModelPredictiveController.ConstrainCommand(0.2, 0.4, true, 0, 1, -0.1, 0.1)
            .Should().BeApproximately(0.3, 1e-12);

        // Value clamp happens before the rate limit.
        ModelPredictiveController.ConstrainCommand(5, 0.4, true, 0, 1, -0.2, 0.2)
            .Should().BeApproximately(0.6, 1e-12);
        ModelPredictiveController.ConstrainCommand(-5, 0.4, true, 0, 1, -0.2, 0.2)
            .Should().BeApproximately(0.2, 1e-12);

        // No previous command, so the rate window does not apply.
        ModelPredictiveController.ConstrainCommand(5, 0, false, 0, 1, -0.01, 0.01)
            .Should().Be(1);
        ModelPredictiveController.ConstrainCommand(0.25, 0, false, 0, 1, -0.01, 0.01)
            .Should().BeApproximately(0.25, 1e-12);
    }

    [Fact]
    public void ConstrainCommand_UnusableBounds_HoldInsteadOfCommandingNaN()
    {
        ModelPredictiveController.ConstrainCommand(0.5, 0.4, true, double.NaN, 1, -0.1, 0.1)
            .Should().Be(0.4);
        ModelPredictiveController.ConstrainCommand(0.5, 0.4, true, 1, 0, -0.1, 0.1)
            .Should().Be(0.4);
        ModelPredictiveController.ConstrainCommand(0.5, double.NaN, true, double.NaN, 1, -0.1, 0.1)
            .Should().Be(0);
        ModelPredictiveController.ConstrainCommand(5, 0, false, double.NaN, double.NaN, -0.1, 0.1)
            .Should().Be(0);
    }

    [Fact]
    public void ConstrainCommand_UnusableRateWindow_StillClampsAFiniteCommand()
    {
        ModelPredictiveController.ConstrainCommand(5, 0.4, true, 0, 1, double.NaN, 0.1)
            .Should().Be(1);
        ModelPredictiveController.ConstrainCommand(0.5, 0.4, true, 0, 1, double.PositiveInfinity, double.NaN)
            .Should().BeApproximately(0.5, 1e-12);
    }

    [Fact]
    public void SetConstraints_RejectsNonFiniteOrInvertedLimits()
    {
        var controller = CreateController();
        controller.SetConstraints(new ControlConstraints
        {
            MinValue = 0.2,
            MaxValue = 0.8,
            MinRate = -1,
            MaxRate = 1
        });

        var act = () => controller.SetConstraints(new ControlConstraints
        {
            MinValue = double.NaN,
            MaxValue = 1,
            MinRate = -1,
            MaxRate = 1
        });

        act.Should().Throw<ArgumentOutOfRangeException>();

        controller.Invoking(c => c.SetConstraints(new ControlConstraints
        {
            MinValue = 1,
            MaxValue = 0,
            MinRate = -1,
            MaxRate = 1
        })).Should().Throw<ArgumentOutOfRangeException>();

        controller.Invoking(c => c.SetConstraints(new ControlConstraints
        {
            MinValue = 0,
            MaxValue = 1,
            MinRate = 5,
            MaxRate = double.PositiveInfinity
        })).Should().Throw<ArgumentOutOfRangeException>();

        controller.Invoking(c => c.SetConstraints(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetReferenceTrajectory_RejectsANonFiniteValue()
    {
        var controller = CreateController(predictionHorizon: 3);

        var act = () => controller.SetReferenceTrajectory(new[] { 0.1, double.NaN, 0.2 });

        act.Should().Throw<ArgumentOutOfRangeException>();
        controller.Invoking(c => c.SetReferenceTrajectory(null!))
            .Should().Throw<ArgumentNullException>();
        controller.Invoking(c => c.SetReferenceTrajectory(new[] { 0.1, 0.2 }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RejectsANonFiniteSamplingTime()
    {
        var act = () => CreateController(samplingTime: double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task NaNSensor_CommandsAFinitePositionInsideTheActuatorRange()
    {
        var actuator = new RecordingActuator();
        using var controller = CreateController(actuator, new ScriptedSensor(double.NaN));

        await controller.StartAsync();
        var completed = await Task.WhenAny(actuator.FirstCommand, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(actuator.FirstCommand);
        await controller.StopAsync();

        actuator.Positions.Should().NotBeEmpty();
        actuator.Positions.Should().OnlyContain(position => double.IsFinite(position) && position >= 0 && position <= 1);
    }

    [Fact]
    public async Task FiniteSensor_CommandsAFinitePositionInsideTheActuatorRange()
    {
        var actuator = new RecordingActuator();
        using var controller = CreateController(actuator, new ScriptedSensor(0));

        await controller.StartAsync();
        var completed = await Task.WhenAny(actuator.FirstCommand, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(actuator.FirstCommand);
        await controller.StopAsync();

        actuator.Positions.Should().NotBeEmpty();
        actuator.Positions.Should().OnlyContain(position => double.IsFinite(position) && position >= 0 && position <= 1);
    }

    private static ModelPredictiveController CreateController(
        IActuator? actuator = null,
        ISensor<double>? sensor = null,
        int predictionHorizon = 4,
        double samplingTime = 0.01)
    {
        return new ModelPredictiveController(
            actuator ?? new RecordingActuator(),
            new List<ISensor<double>> { sensor ?? new ScriptedSensor(0) },
            new EngineModel(1, 1),
            predictionHorizon: predictionHorizon,
            controlHorizon: 2,
            samplingTime: samplingTime,
            frequencyHz: 100);
    }

    private sealed class RecordingActuator : IActuator
    {
        private readonly object _gate = new();
        private readonly List<double> _positions = new();
        private readonly TaskCompletionSource<double> _first =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<double> FirstCommand => _first.Task;

        public IReadOnlyList<double> Positions
        {
            get
            {
                lock (_gate)
                {
                    return _positions.ToArray();
                }
            }
        }

        public string ActuatorId => "mpc";
        public string Name => "mpc";
        public ActuatorType Type => ActuatorType.Throttle;
        public ActuatorStatus Status => ActuatorStatus.Ready;
        public double MinPosition => 0;
        public double MaxPosition => 1;
        public double ResponseTimeSeconds => 0.01;
        public double MaxRateOfChange => 10;
        public bool IsEnabled => true;

        public event EventHandler<ActuatorPositionChangedEventArgs>? PositionChanged;

        public Task<bool> SetPositionAsync(double position, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _positions.Add(position);
            }

            _first.TrySetResult(position);
            PositionChanged?.Invoke(this, new ActuatorPositionChangedEventArgs());
            return Task.FromResult(true);
        }

        public Task<double> GetPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0d);

        public Task<bool> EnableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> DisableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class ScriptedSensor : ISensor<double>
    {
        private readonly double _value;

        public ScriptedSensor(double value)
        {
            _value = value;
        }

        public string SensorId => "state";
        public string Name => "state";
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
            return Task.FromResult(_value);
        }

        public Task<bool> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
