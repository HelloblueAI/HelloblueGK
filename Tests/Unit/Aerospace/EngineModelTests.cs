using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HB_NLP_Research_Lab.Aerospace;
using Xunit;

namespace HelloblueGK.Tests.Unit.Aerospace;

public class EngineModelTests
{
    /// <summary>
    /// Every engine model must report its performance envelope through RocketEngineBase,
    /// because that is how anything comparing engines sees them.
    ///
    /// RaptorEngine previously redeclared Name, Thrust, SpecificImpulse, ChamberPressure,
    /// and Propellant with `new`. That shadows instead of overriding, so the constructor
    /// filled the derived copies while the inherited ones stayed at their defaults. Reading
    /// a Raptor through a RocketEngineBase reference reported zero thrust and a blank name.
    /// Nothing caught it because the tests below read the engines through their concrete
    /// types, which is exactly the view where the shadowed copy looks correct.
    /// </summary>
    [Fact]
    public void AllEngineModels_ReportTheirEnvelopeThroughTheBaseClass()
    {
        var engines = new List<RocketEngineBase>
        {
            new MerlinEngine(),
            new RaptorEngine(),
            new RS25Engine()
        };

        foreach (var engine in engines)
        {
            engine.Name.Should().NotBeNullOrWhiteSpace(
                "an engine that cannot name itself polymorphically cannot be reported on");
            engine.Propellant.Should().NotBeNullOrWhiteSpace();
            engine.Thrust.Should().BeGreaterThan(0);
            engine.SpecificImpulse.Should().BeGreaterThan(0);
            engine.ChamberPressure.Should().BeGreaterThan(0);
        }
    }

    /// <summary>
    /// The concrete and inherited views must agree. If they diverge, a shadowing copy has
    /// been reintroduced and half the codebase is reading stale defaults.
    /// </summary>
    [Fact]
    public void RaptorEngine_ConcreteAndInheritedViewsAgree()
    {
        var raptor = new RaptorEngine();
        RocketEngineBase asBase = raptor;

        asBase.Name.Should().Be(raptor.Name);
        asBase.Propellant.Should().Be(raptor.Propellant);
        asBase.Thrust.Should().Be(raptor.Thrust);
        asBase.SpecificImpulse.Should().Be(raptor.SpecificImpulse);
        asBase.ChamberPressure.Should().Be(raptor.ChamberPressure);
    }

    /// <summary>
    /// Selecting the highest-thrust engine is the simplest thing a caller would do with a
    /// collection of engines, and it was silently wrong: Raptor reported zero thrust through
    /// the base class, so the 845 kN Merlin outranked the 2.2 MN Raptor.
    /// </summary>
    [Fact]
    public void HighestThrustEngine_SelectedThroughBaseClass_IsRaptor()
    {
        var engines = new List<RocketEngineBase>
        {
            new MerlinEngine(),
            new RaptorEngine(),
            new RS25Engine()
        };

        engines.OrderByDescending(engine => engine.Thrust).First()
            .Should().BeOfType<RaptorEngine>();
    }

    /// <summary>
    /// Same shadowing defect, different class: the architecture registry stores engines as
    /// RevolutionaryEngine, so a shadowed Name left every registered variable-geometry
    /// engine anonymous when read back.
    /// </summary>
    [Fact]
    public void VariableGeometryEngine_ReportsItsNameThroughItsBaseClass()
    {
        RevolutionaryEngine engine = new VariableGeometryEngine();

        engine.Name.Should().Be("Variable Geometry Engine");
    }

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
