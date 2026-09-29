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
