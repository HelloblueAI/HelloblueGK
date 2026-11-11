using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Models;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Demo types used to return fixed efficiency, reliability, and solver-selection
/// strings. Those fields stay unset unless a caller supplies a measured value.
/// </summary>
public class UnmeasuredDemoScoreTests
{
    [Fact]
    public async Task AdvancedEngineCore_DoesNotReturnAMeasuredEfficiency()
    {
        var core = new AdvancedEngineCore(new EngineConfig());

        (await core.StartEngineAsync()).Efficiency.Should().Be(0);
        (await core.OptimizePerformanceAsync()).Efficiency.Should().Be(0);
        (await new NozzleComponent().GetStatusAsync()).Efficiency.Should().Be(0);
        (await core.GetEngineStatusAsync()).Health.Should().BeEmpty();
        (await core.GetEngineStatusAsync()).Performance.Should().BeEmpty();
    }

    [Fact]
    public async Task AutonomousDesigner_DoesNotAssignReliability()
    {
        var engine = await new AutonomousEngineDesigner().GenerateEngineAsync("demo");
        engine.Performance.Reliability.Should().Be(0);

        var optimized = await new OptimizationAlgorithm().OptimizeAsync(new EngineArchitecture());
        optimized.Performance.Reliability.Should().Be(0);
    }

    [Fact]
    public void ParameterDefaults_DoNotNameAnAlgorithmOrCouplingMethod()
    {
        new OptimizationParameters().OptimizationAlgorithm.Should().BeEmpty();
        new MultiPhysicsParameters().CouplingMethod.Should().BeEmpty();
    }
}
