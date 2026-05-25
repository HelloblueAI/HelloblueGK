using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class EngineRepositorySecurityTests
{
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
