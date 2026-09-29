using HB_NLP_Research_Lab.Core.Control;

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
}
