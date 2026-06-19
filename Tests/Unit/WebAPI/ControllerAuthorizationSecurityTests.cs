using System.Security.Claims;
using System.Text.Json;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class ControllerAuthorizationSecurityTests
{
    [Fact]
    public async Task GetAllOptimizations_ForStandardUser_ReturnsOnlyOwnedRuns()
    {
        await using var context = CreateContext();
        await SeedOptimizationAsync(context, "alice");
        await SeedOptimizationAsync(context, "bob");

        var controller = CreateOptimizationController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllOptimizations();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var optimizations = okResult.Value.Should()
            .BeAssignableTo<IEnumerable<AIOptimizationRunResponse>>()
            .Subject
            .ToList();

        optimizations.Should().ContainSingle();
        optimizations[0].CreatedBy.Should().Be("alice");
    }

    [Fact]
    public async Task GetOptimizationStatus_ForDifferentStandardUser_ReturnsForbid()
    {
        await using var context = CreateContext();
        var optimization = await SeedOptimizationAsync(context, "alice");

        var controller = CreateOptimizationController(context, CreatePrincipal("bob"));

        var result = await controller.GetOptimizationStatus(optimization.Id);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task StartOptimization_ForEngineOwnedByDifferentUser_ReturnsForbidWithoutCreatingRun()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateOptimizationController(context, CreatePrincipal("bob"));

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic"
        });

        result.Should().BeOfType<ForbidResult>();
        context.AIOptimizationRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOptimizationStatus_ForFailedRun_DoesNotExposeStoredException()
    {
        await using var context = CreateContext();
        var optimization = await SeedOptimizationAsync(
            context,
            "alice",
            status: "Failed",
            errorMessage: "database stack trace /srv/app/secret");

        var controller = CreateOptimizationController(context, CreatePrincipal("alice"));

        var result = await controller.GetOptimizationStatus(optimization.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseJson = JsonSerializer.Serialize(okResult.Value);

        responseJson.Should().Contain("Optimization failed. See server logs for details.");
        responseJson.Should().NotContain("database stack trace");
        responseJson.Should().NotContain("/srv/app/secret");
    }

    [Fact]
    public async Task GetAllDigitalTwins_ForStandardUser_ReturnsOnlyOwnedTwins()
    {
        await using var context = CreateContext();
        await SeedDigitalTwinAsync(context, "alice");
        await SeedDigitalTwinAsync(context, "bob");

        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllDigitalTwins();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var digitalTwins = okResult.Value.Should()
            .BeAssignableTo<IEnumerable<DigitalTwinResponse>>()
            .Subject
            .ToList();

        digitalTwins.Should().ContainSingle();
        digitalTwins[0].CreatedBy.Should().Be("alice");
    }

    [Fact]
    public async Task GetPredictions_ForDifferentStandardUser_ReturnsForbid()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "alice");

        var controller = CreateDigitalTwinController(context, CreatePrincipal("bob"));

        var result = await controller.GetPredictions(digitalTwin.Id, new PredictionRequest());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateDigitalTwin_ForEngineOwnedByDifferentUser_ReturnsForbidWithoutCreatingTwin()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("bob"));

        var result = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Unauthorized twin"
        });

        result.Should().BeOfType<ForbidResult>();
        context.DigitalTwins.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDigitalTwinById_DoesNotExposeFullEngineMetadata()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Private Engine",
            EngineType = "Test",
            CreatedBy = "engine-owner",
            Thrust = 42
        };
        var digitalTwin = new DigitalTwin
        {
            Engine = engine,
            Name = "Alice twin",
            PredictionAccuracy = 0.99,
            CreatedBy = "alice",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        context.DigitalTwins.Add(digitalTwin);
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));

        var result = await controller.GetDigitalTwinById(digitalTwin.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DigitalTwinResponse>().Subject;
        response.Engine.Should().NotBeNull();
        response.Engine!.Id.Should().Be(engine.Id);
        response.Engine.Name.Should().Be(engine.Name);
        response.Engine.EngineType.Should().Be(engine.EngineType);

        var responseJson = JsonSerializer.Serialize(response);
        responseJson.Should().NotContain("engine-owner");
        responseJson.Should().NotContain(nameof(Engine.Thrust));
    }

    [Fact]
    public async Task GetAllLaunches_ForStandardUser_ReturnsOnlyOwnedLaunches()
    {
        await using var context = CreateContext();
        await SeedLaunchAsync(context, "alice");
        await SeedLaunchAsync(context, "bob");

        var controller = CreateLaunchesController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllLaunches();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var launches = okResult.Value.Should()
            .BeAssignableTo<IEnumerable<LaunchResponse>>()
            .Subject
            .ToList();

        launches.Should().ContainSingle();
        launches[0].CreatedBy.Should().Be("alice");
    }

    [Fact]
    public async Task GetLaunchById_ForDifferentStandardUser_ReturnsForbid()
    {
        await using var context = CreateContext();
        var launch = await SeedLaunchAsync(context, "alice");

        var controller = CreateLaunchesController(context, CreatePrincipal("bob"));

        var result = await controller.GetLaunchById(launch.Id);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetLaunchStatistics_ForStandardUser_CountsOnlyOwnedLaunches()
    {
        await using var context = CreateContext();
        await SeedLaunchAsync(context, "alice", missionSuccess: true);
        await SeedLaunchAsync(context, "bob", missionSuccess: false);

        var controller = CreateLaunchesController(context, CreatePrincipal("alice"));

        var result = await controller.GetLaunchStatistics();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseJson = JsonSerializer.Serialize(okResult.Value);

        responseJson.Should().Contain("\"totalLaunches\":1");
        responseJson.Should().Contain("\"successful\":1");
        responseJson.Should().Contain("\"failed\":0");
    }

    [Fact]
    public void LaunchResponse_ForFailedInternalError_DoesNotExposeStoredException()
    {
        var launch = new Launch
        {
            MissionName = "Secret Failure",
            Status = "Failed",
            ErrorMessage = "SQL connection failed for user admin",
            ScheduledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var response = LaunchResponse.FromEntity(launch);

        response.ErrorMessage.Should().Be("Launch failed. See server logs for details.");
    }

    private static HelloblueGKDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HelloblueGKDbContext(options);
    }

    private static async Task<AIOptimizationRun> SeedOptimizationAsync(
        HelloblueGKDbContext context,
        string createdBy,
        string status = "Completed",
        string? errorMessage = null)
    {
        var engine = CreateEngine(createdBy);
        var optimization = new AIOptimizationRun
        {
            Engine = engine,
            AlgorithmType = "Genetic",
            Status = status,
            CreatedBy = createdBy,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        context.AIOptimizationRuns.Add(optimization);
        await context.SaveChangesAsync();
        return optimization;
    }

    private static async Task<DigitalTwin> SeedDigitalTwinAsync(
        HelloblueGKDbContext context,
        string createdBy)
    {
        var engine = CreateEngine(createdBy);
        var digitalTwin = new DigitalTwin
        {
            Engine = engine,
            Name = $"{createdBy} twin",
            PredictionAccuracy = 0.99,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.DigitalTwins.Add(digitalTwin);
        await context.SaveChangesAsync();
        return digitalTwin;
    }

    private static async Task<Launch> SeedLaunchAsync(
        HelloblueGKDbContext context,
        string createdBy,
        bool? missionSuccess = null)
    {
        var engine = CreateEngine(createdBy);
        var launch = new Launch
        {
            Engine = engine,
            MissionName = $"{createdBy} mission",
            Status = missionSuccess.HasValue ? (missionSuccess.Value ? "Success" : "Failed") : "Scheduled",
            MissionSuccess = missionSuccess,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        context.Launches.Add(launch);
        await context.SaveChangesAsync();
        return launch;
    }

    private static Engine CreateEngine(string owner)
    {
        return new Engine
        {
            Name = $"{owner}-engine-{Guid.NewGuid():N}",
            EngineType = "Test",
            CreatedBy = owner,
            Thrust = 1,
            SpecificImpulse = 1,
            ChamberPressure = 1,
            Efficiency = 0.95,
            ExpansionRatio = 1,
            MassFlowRate = 1
        };
    }

    private static AIOptimizationController CreateOptimizationController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user)
    {
        return new AIOptimizationController(
            context,
            new AdvancedAIOptimizationEngine(),
            NullLogger<AIOptimizationController>.Instance,
            new ServiceCollection().BuildServiceProvider())
        {
            ControllerContext = CreateControllerContext(user)
        };
    }

    private static DigitalTwinController CreateDigitalTwinController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user)
    {
        return new DigitalTwinController(
            context,
            new DigitalTwinEngine(),
            NullLogger<DigitalTwinController>.Instance)
        {
            ControllerContext = CreateControllerContext(user)
        };
    }

    private static LaunchesController CreateLaunchesController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user)
    {
        return new LaunchesController(
            context,
            new HelloblueGKEngine(),
            NullLogger<LaunchesController>.Instance,
            new ServiceCollection().BuildServiceProvider())
        {
            ControllerContext = CreateControllerContext(user)
        };
    }

    private static ControllerContext CreateControllerContext(ClaimsPrincipal user)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string username, bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new("username", username),
            new(ClaimTypes.Role, isAdmin ? "Admin" : "User")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
