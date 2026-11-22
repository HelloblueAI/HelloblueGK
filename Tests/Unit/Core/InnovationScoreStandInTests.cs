using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Generated architectures used to publish a random innovation level from 0.5 to 1.0.
/// That draw was averaged into the design score. Nothing was measured, so the level
/// and the score stay unset.
/// </summary>
public class InnovationScoreStandInTests
{
    [Fact]
    public async Task RevolutionaryDesign_DoesNotPublishARandomInnovationScore()
    {
        var first = await new AutonomousEngineDesigner()
            .GenerateRevolutionaryEngineAsync(new DesignRequirements());
        var second = await new AutonomousEngineDesigner()
            .GenerateRevolutionaryEngineAsync(new DesignRequirements());

        first.AllArchitectures.Should().NotBeEmpty();
        first.AllArchitectures.Should().OnlyContain(architecture => architecture.InnovationLevel == 0);
        first.OptimizedEngines.Should().OnlyContain(engine =>
            engine.Architecture.InnovationLevel == 0
            && engine.InnovationLevel == 0);
        first.BestEngine.Architecture.InnovationLevel.Should().Be(0);
        first.BestEngine.InnovationLevel.Should().Be(0);
        first.DesignInnovationScore.Should().Be(0);
        second.AllArchitectures.Should().OnlyContain(architecture => architecture.InnovationLevel == 0);
        second.DesignInnovationScore.Should().Be(0);
    }
}
