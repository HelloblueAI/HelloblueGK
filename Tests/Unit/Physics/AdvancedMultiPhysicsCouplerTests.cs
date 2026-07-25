using FluentAssertions;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Physics;

namespace HelloblueGK.Tests.Unit.Physics;

public class AdvancedMultiPhysicsCouplerTests
{
    [Fact]
    public async Task RunCoupledAnalysisAsync_ConcurrentCalls_ReturnIndependentHistories()
    {
        var coupler = new AdvancedMultiPhysicsCoupler();
        await coupler.InitializeAsync();

        var engineA = new EngineModel
        {
            Name = "CouplerEngineA",
            Parameters = new Dictionary<string, double> { ["Thrust"] = 1_000_000 }
        };
        var engineB = new EngineModel
        {
            Name = "CouplerEngineB",
            Parameters = new Dictionary<string, double> { ["Thrust"] = 1_200_000 }
        };

        var results = await Task.WhenAll(
            coupler.RunCoupledAnalysisAsync(engineA),
            coupler.RunCoupledAnalysisAsync(engineB));

        results.Should().HaveCount(2);
        results.Should().OnlyContain(result => result.CouplingHistory != null);
        results[0].CouplingHistory.Should().NotBeSameAs(results[1].CouplingHistory);
        results.Should().OnlyContain(result => result.TotalIterations > 0);
    }
}
