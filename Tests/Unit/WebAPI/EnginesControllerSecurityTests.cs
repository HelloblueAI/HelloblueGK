using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class EnginesControllerSecurityTests
{
    [Fact]
    public async Task GetAllEngines_ForStandardUser_ReturnsOnlySharedAndOwnedEngines()
    {
        var options = CreateOptions();
        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-3), "shared-catalog", createdBy: null));
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-2), "alice-private", createdBy: "alice"));
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-1), "bob-private", createdBy: "bob"));
            await seedContext.SaveChangesAsync();
        }

        await using (var readContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(readContext, CreatePrincipal("alice", isAdmin: false));

            var result = await controller.GetAllEngines();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var engines = okResult.Value.Should().BeAssignableTo<IEnumerable<Engine>>().Subject.ToList();
            engines.Select(engine => engine.Name)
                .Should().BeEquivalentTo("shared-catalog", "alice-private");
            engines.Should().NotContain(engine => engine.CreatedBy == "bob");
        }
    }

    [Fact]
    public async Task GetActiveEngines_ForStandardUser_ReturnsOnlyActiveSharedAndOwnedEngines()
    {
        var options = CreateOptions();
        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-4), "shared-active", createdBy: null));
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-3), "alice-active", createdBy: "alice"));
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-2), "alice-inactive", createdBy: "alice", isActive: false));
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow.AddMinutes(-1), "bob-active", createdBy: "bob"));
            await seedContext.SaveChangesAsync();
        }

        await using (var readContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(readContext, CreatePrincipal("alice", isAdmin: false));

            var result = await controller.GetActiveEngines();

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var engines = okResult.Value.Should().BeAssignableTo<IEnumerable<Engine>>().Subject.ToList();
            engines.Select(engine => engine.Name)
                .Should().BeEquivalentTo("shared-active", "alice-active");
        }
    }

    [Fact]
    public async Task GetEngineById_ForDifferentStandardUser_ReturnsForbid()
    {
        var options = CreateOptions();
        int engineId;
        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            var engine = CreateEngine(DateTime.UtcNow, "alice-private", createdBy: "alice");
            seedContext.Engines.Add(engine);
            await seedContext.SaveChangesAsync();
            engineId = engine.Id;
        }

        await using (var readContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(readContext, CreatePrincipal("bob", isAdmin: false));

            var result = await controller.GetEngineById(engineId);

            result.Should().BeOfType<ForbidResult>();
        }
    }

    [Fact]
    public async Task GetEngineByName_ForDifferentStandardUser_ReturnsForbid()
    {
        var options = CreateOptions();
        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            seedContext.Engines.Add(CreateEngine(DateTime.UtcNow, "alice-private", createdBy: "alice"));
            await seedContext.SaveChangesAsync();
        }

        await using (var readContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(readContext, CreatePrincipal("bob", isAdmin: false));

            var result = await controller.GetEngineByName("alice-private");

            result.Should().BeOfType<ForbidResult>();
        }
    }

    [Fact]
    public async Task CreateEngine_IgnoresClientControlledMetadataAndUsesAuthenticatedAdmin()
    {
        var options = CreateOptions();
        int engineId;

        await using (var createContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(createContext, CreatePrincipal("real-admin"));
            var request = new CreateEngineRequest
            {
                Name = "New Engine",
                EngineType = "Raptor",
                Thrust = 2_100_000,
                SpecificImpulse = 375,
                ChamberPressure = 290,
                ExpansionRatio = 35,
                Efficiency = 0.96,
                Propellant = "Methalox",
                MixtureRatio = 3.5,
                MassFlowRate = 625,
                Description = "Server-owned metadata should be enforced"
            };

            var result = await controller.CreateEngine(request);

            var createdAtAction = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var created = createdAtAction.Value.Should().BeOfType<Engine>().Subject;
            engineId = created.Id;
            created.CreatedBy.Should().Be("real-admin");
            created.IsActive.Should().BeTrue();
        }

        await using (var verifyContext = new HelloblueGKDbContext(options))
        {
            var stored = await verifyContext.Engines.SingleAsync(e => e.Id == engineId);

            stored.Name.Should().Be("New Engine");
            stored.CreatedBy.Should().Be("real-admin");
            stored.IsActive.Should().BeTrue();
            stored.UpdatedAt.Should().BeNull();
            stored.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task UpdateEngine_WhenFieldsAreOmitted_PreservesExistingEngineParameters()
    {
        var options = CreateOptions();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        int engineId;

        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            var engine = CreateEngine(createdAt);
            seedContext.Engines.Add(engine);
            await seedContext.SaveChangesAsync();
            engineId = engine.Id;
        }

        await using (var updateContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(updateContext);
            var request = new UpdateEngineRequest
            {
                Id = engineId,
                Name = "Renamed Engine",
                Thrust = 0
            };

            var result = await controller.UpdateEngine(engineId, request);

            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var updated = okResult.Value.Should().BeOfType<Engine>().Subject;
            updated.Name.Should().Be("Renamed Engine");
            updated.Thrust.Should().Be(0);
        }

        await using (var verifyContext = new HelloblueGKDbContext(options))
        {
            var stored = await verifyContext.Engines.SingleAsync(e => e.Id == engineId);

            stored.Name.Should().Be("Renamed Engine");
            stored.EngineType.Should().Be("Raptor");
            stored.Thrust.Should().Be(0);
            stored.SpecificImpulse.Should().Be(380);
            stored.ChamberPressure.Should().Be(300);
            stored.ExpansionRatio.Should().Be(40);
            stored.Efficiency.Should().Be(0.97);
            stored.Propellant.Should().Be("Methalox");
            stored.MixtureRatio.Should().Be(3.6);
            stored.MassFlowRate.Should().Be(650);
            stored.Description.Should().Be("Flight-rated baseline");
            stored.CreatedBy.Should().Be("admin");
            stored.CreatedAt.Should().Be(createdAt);
            stored.IsActive.Should().BeTrue();
            stored.UpdatedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UpdateEngine_WhenBodyIdMismatchesRouteId_ReturnsBadRequestWithoutChangingEngine()
    {
        var options = CreateOptions();
        int engineId;

        await using (var seedContext = new HelloblueGKDbContext(options))
        {
            var engine = CreateEngine(DateTime.UtcNow.AddDays(-1));
            seedContext.Engines.Add(engine);
            await seedContext.SaveChangesAsync();
            engineId = engine.Id;
        }

        await using (var updateContext = new HelloblueGKDbContext(options))
        {
            var controller = CreateController(updateContext);

            var result = await controller.UpdateEngine(engineId, new UpdateEngineRequest
            {
                Id = engineId + 1,
                Name = "Should Not Persist"
            });

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        await using (var verifyContext = new HelloblueGKDbContext(options))
        {
            var stored = await verifyContext.Engines.SingleAsync(e => e.Id == engineId);
            stored.Name.Should().Be("Baseline Engine");
            stored.UpdatedAt.Should().BeNull();
        }
    }

    private static DbContextOptions<HelloblueGKDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private static EnginesController CreateController(HelloblueGKDbContext context, ClaimsPrincipal? user = null)
    {
        return new EnginesController(
            new EngineRepository(context),
            NullLogger<EnginesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string username, bool isAdmin = true)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("username", username),
            new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static Engine CreateEngine(
        DateTime createdAt,
        string name = "Baseline Engine",
        string? createdBy = "admin",
        bool isActive = true)
    {
        return new Engine
        {
            Name = name,
            EngineType = "Raptor",
            Thrust = 2_300_000,
            SpecificImpulse = 380,
            ChamberPressure = 300,
            ExpansionRatio = 40,
            Efficiency = 0.97,
            Propellant = "Methalox",
            MixtureRatio = 3.6,
            MassFlowRate = 650,
            Description = "Flight-rated baseline",
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            IsActive = isActive
        };
    }
}
