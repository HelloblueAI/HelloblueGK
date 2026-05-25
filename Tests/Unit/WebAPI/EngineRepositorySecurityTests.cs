using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class EngineRepositorySecurityTests
{
    [Fact]
    public void EngineSerialization_ExcludesNestedResourceCollections()
    {
        var engine = new Engine
        {
            Id = 1,
            Name = "Shared Engine",
            EngineType = "Test",
            Simulations =
            [
                new EngineSimulation
                {
                    Id = 10,
                    SimulationType = "CFD",
                    ParametersJson = "{\"secret\":\"alice-parameters\"}",
                    ResultsJson = "{\"secret\":\"alice-results\"}"
                }
            ],
            OptimizationRuns =
            [
                new AIOptimizationRun
                {
                    Id = 20,
                    AlgorithmType = "Genetic",
                    ParametersJson = "{\"secret\":\"optimization-parameters\"}",
                    ResultsJson = "{\"secret\":\"optimization-results\"}"
                }
            ],
            DigitalTwins =
            [
                new DigitalTwin
                {
                    Id = 30,
                    Name = "Private Twin",
                    ModelDataJson = "{\"secret\":\"model-data\"}"
                }
            ]
        };

        var json = JsonSerializer.Serialize(engine);

        json.Should().NotContain(nameof(Engine.Simulations));
        json.Should().NotContain(nameof(Engine.OptimizationRuns));
        json.Should().NotContain(nameof(Engine.DigitalTwins));
        json.Should().NotContain("alice-parameters");
        json.Should().NotContain("optimization-parameters");
        json.Should().NotContain("model-data");
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotLoadSimulations()
    {
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        int engineId;
        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            var engine = new Engine
            {
                Name = "Shared Engine",
                EngineType = "Test",
                CreatedBy = "admin"
            };

            seedContext.Engines.Add(engine);
            seedContext.EngineSimulations.Add(new EngineSimulation
            {
                Engine = engine,
                SimulationType = "CFD",
                Status = "Completed",
                CreatedBy = "alice",
                ParametersJson = "{\"secret\":\"alice-parameters\"}",
                ResultsJson = "{\"secret\":\"alice-results\"}"
            });

            await seedContext.SaveChangesAsync();
            engineId = engine.Id;
        }

        await using var context = new HelloblueGKDbContext(options);
        var repository = new EngineRepository(context);

        var result = await repository.GetByIdAsync(engineId);

        result.Should().NotBeNull();
        result!.Simulations.Should().BeEmpty(
            "engine detail responses must not bypass simulation ownership filtering");
        context.Entry(result).Collection(e => e.Simulations).IsLoaded.Should().BeFalse();
    }
}
