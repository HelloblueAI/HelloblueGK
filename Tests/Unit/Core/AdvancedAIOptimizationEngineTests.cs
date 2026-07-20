using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class AdvancedAIOptimizationEngineTests
{
    [Fact]
    public async Task ConcurrentIdenticalOptimizations_ShouldShareCachedOperation()
    {
        // Arrange
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_500_000,
            SpecificImpulse = 380,
            ChamberPressure = 20_000_000,
            Efficiency = 0.92
        };

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 32)
                .Select(_ => engine.OptimizeEngineDesignAsync(parameters)));

        // Assert
        results.Should().OnlyContain(result => ReferenceEquals(result, results[0]));
    }
}
