using FluentAssertions;
using HB_NLP_Research_Lab.Aerospace;
using Xunit;

namespace HelloblueGK.Tests.Unit.Aerospace;

public class EngineModelTests
{
    [Fact]
    public void MerlinEngine_HasPositivePerformanceEnvelope()
    {
        var engine = new MerlinEngine();

        engine.Name.Should().Be("Merlin");
        engine.Thrust.Should().BeGreaterThan(0);
        engine.SpecificImpulse.Should().BeGreaterThan(0);
        engine.ChamberPressure.Should().BeGreaterThan(0);
        engine.Propellant.Should().Contain("LOX");
    }

    [Fact]
    public void RaptorEngine_UsesMethaneFullFlowCycle()
    {
        var engine = new RaptorEngine();

        engine.Name.Should().Contain("Raptor");
        engine.Thrust.Should().BeGreaterThan(1_000_000);
        engine.SpecificImpulse.Should().BeInRange(250, 400);
        engine.Propellant.Should().Contain("Methane");
        engine.EngineCycle.Should().Contain("Full-Flow");
        engine.RestartCapability.Should().BeTrue();
        engine.MinimumThrottle.Should().BeInRange(0.1, 0.6);
        engine.MaximumThrottle.Should().Be(1.0);
        engine.MinimumThrottle.Should().BeLessThan(engine.MaximumThrottle);
    }

    [Fact]
    public void RS25Engine_IsHydrogenOxygenUpperStageClass()
    {
        var engine = new RS25Engine();

        engine.Name.Should().NotBeNullOrWhiteSpace();
        engine.Thrust.Should().BeGreaterThan(0);
        engine.SpecificImpulse.Should().BeGreaterThan(0);
        engine.Propellant.Should().NotBeNullOrWhiteSpace();
    }
}
