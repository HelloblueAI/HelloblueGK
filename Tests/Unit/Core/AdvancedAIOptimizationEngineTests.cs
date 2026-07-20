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

    [Fact]
    public async Task ManyUniqueOptimizations_ShouldKeepCacheBounded()
    {
        // Arrange
        var engine = new AdvancedAIOptimizationEngine();
        var requests = Enumerable.Range(0, 300)
            .Select(index => new EngineDesignParameters
            {
                Thrust = 1_500_000 + index,
                SpecificImpulse = 380,
                ChamberPressure = 20_000_000,
                Efficiency = 0.92
            });

        // Act
        await Task.WhenAll(requests.Select(engine.OptimizeEngineDesignAsync));

        // Assert
        var cacheField = typeof(AdvancedAIOptimizationEngine)
            .GetField("_optimizationCache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        cacheField.Should().NotBeNull();
        var cache = cacheField!.GetValue(engine);
        var cacheCount = (int)cache!.GetType().GetProperty("Count")!.GetValue(cache)!;
        cacheCount.Should().BeLessThanOrEqualTo(256);
    }
}
