using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Generated control systems used to publish 99.9-100% accuracy and a 1-3 ms
/// response. Nothing was measured, so those fields stay unset.
/// </summary>
public class ControlSystemStandInTests
{
    [Fact]
    public async Task RevolutionaryDesign_DoesNotPublishControlAccuracyOrResponseTime()
    {
        var result = await new AutonomousEngineDesigner()
            .GenerateRevolutionaryEngineAsync(new DesignRequirements());

        result.AllArchitectures.Should().NotBeEmpty();
        result.AllArchitectures.Should().OnlyContain(architecture =>
            architecture.ControlSystem.Accuracy == 0
            && architecture.ControlSystem.ResponseTime == 0
            && architecture.ControlSystem.Type == "AI-Driven Adaptive Control");
        result.OptimizedEngines.Should().OnlyContain(engine =>
            engine.Architecture.ControlSystem.Accuracy == 0
            && engine.Architecture.ControlSystem.ResponseTime == 0);
        result.BestEngine.Architecture.ControlSystem.Accuracy.Should().Be(0);
        result.BestEngine.Architecture.ControlSystem.ResponseTime.Should().Be(0);
    }
}
