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
        cacheCount.Should().Be(256);

        var uncachedParameters = new EngineDesignParameters
        {
            Thrust = 2_000_000,
            SpecificImpulse = 390,
            ChamberPressure = 21_000_000,
            Efficiency = 0.93
        };
        var concurrentResults = await Task.WhenAll(
            engine.OptimizeEngineDesignAsync(uncachedParameters),
            engine.OptimizeEngineDesignAsync(uncachedParameters));

        concurrentResults[0].Should().BeSameAs(concurrentResults[1]);
    }

    [Fact]
    public async Task OptimizeEngineDesignAsync_WithGeneticAlgorithm_RunsOnlyGeneticStage()
    {
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_000_000,
            SpecificImpulse = 350,
            ChamberPressure = 15_000_000,
            Efficiency = 0.9
        };

        var result = await engine.OptimizeEngineDesignAsync(parameters, "Genetic");

        result.AlgorithmType.Should().Be("Genetic");
        result.OptimizationStages.Should().ContainSingle();
        result.OptimizationStages[0].StageName.Should().Be("Genetic Algorithm");
    }

    [Fact]
    public async Task OptimizeEngineDesignAsync_CacheKeysDifferByAlgorithmType()
    {
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_100_000,
            SpecificImpulse = 360,
            ChamberPressure = 16_000_000,
            Efficiency = 0.91
        };

        var genetic = await engine.OptimizeEngineDesignAsync(parameters, "Genetic");
        var neural = await engine.OptimizeEngineDesignAsync(parameters, "NeuralNetwork");

        genetic.Should().NotBeSameAs(neural);
        genetic.AlgorithmType.Should().Be("Genetic");
        neural.AlgorithmType.Should().Be("NeuralNetwork");
        genetic.OptimizationStages.Should().ContainSingle(stage => stage.StageName == "Genetic Algorithm");
        neural.OptimizationStages.Should().ContainSingle(stage => stage.StageName == "Neural Network");
    }

    [Fact]
    public async Task OptimizeEngineDesignAsync_WithCancelledToken_ThrowsOperationCanceled()
    {
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_200_000,
            SpecificImpulse = 370,
            ChamberPressure = 18_000_000,
            Efficiency = 0.9
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await engine.OptimizeEngineDesignAsync(parameters, "Genetic", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConcurrentIdenticalOptimizations_WithNonCancellableTokens_ShouldShareInflightWork()
    {
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_500_000,
            SpecificImpulse = 380,
            ChamberPressure = 20_000_000,
            Efficiency = 0.92
        };

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => engine.OptimizeEngineDesignAsync(
                    parameters,
                    algorithmType: null,
                    CancellationToken.None)));

        results.Should().OnlyContain(result => ReferenceEquals(result, results[0]));
    }

    [Fact]
    public async Task OptimizeEngineDesignAsync_WithLiveCancellableToken_StopsExclusiveWork()
    {
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_500_000,
            SpecificImpulse = 380,
            ChamberPressure = 20_000_000,
            Efficiency = 0.92
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var act = async () => await engine.OptimizeEngineDesignAsync(parameters, "Genetic", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task OptimizeEngineDesignAsync_WithReinforcementLearning_UsesDedicatedRlStage()
    {
        var engine = new AdvancedAIOptimizationEngine();
        var parameters = new EngineDesignParameters
        {
            Thrust = 1_200_000,
            SpecificImpulse = 370,
            ChamberPressure = 18_000_000,
            Efficiency = 0.9
        };

        var result = await engine.OptimizeEngineDesignAsync(parameters, "ReinforcementLearning");

        result.AlgorithmType.Should().Be("ReinforcementLearning");
        result.OptimizationStages.Should().ContainSingle();
        result.OptimizationStages[0].StageName.Should().Be("Reinforcement Learning");
        result.OptimizedParameters.Thrust.Should().BeGreaterThanOrEqualTo(parameters.Thrust);
        result.OptimizedParameters.Efficiency.Should().BeGreaterThanOrEqualTo(parameters.Efficiency);
    }
}
