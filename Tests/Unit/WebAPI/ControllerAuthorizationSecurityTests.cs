using System.Security.Claims;
using System.Text.Json;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Services;
using HB_NLP_Research_Lab.WebAPI.Validation;
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
    public async Task GetAllOptimizations_WithExcessiveTake_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateOptimizationController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllOptimizations(take: 101);

        result.Should().BeOfType<BadRequestObjectResult>();
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
    public async Task StartOptimization_WhenBackgroundQueueIsFull_ReturnsServiceUnavailableWithoutCreatingRun()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("shared");
        engine.CreatedBy = null;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            new RejectingBackgroundWorkQueue());

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.AIOptimizationRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task StartOptimization_WithTooManyParameters_ReturnsBadRequestWithoutCreatingRun()
    {
        await using var context = CreateContext();
        var controller = CreateOptimizationController(context, CreatePrincipal("alice"));

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = 1,
            AlgorithmType = "Genetic",
            Parameters = CreateOversizedObjectDictionary()
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("Parameters");
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
    public async Task GetOptimizationStatus_ForRunningRun_DoesNotExposeStoredDiagnostic()
    {
        await using var context = CreateContext();
        var optimization = await SeedOptimizationAsync(
            context,
            "alice",
            status: "Running",
            errorMessage: "redis connection string and /srv/app/internal-path");

        var controller = CreateOptimizationController(context, CreatePrincipal("alice"));

        var result = await controller.GetOptimizationStatus(optimization.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseJson = JsonSerializer.Serialize(okResult.Value);

        responseJson.Should().NotContain("redis connection string");
        responseJson.Should().NotContain("/srv/app/internal-path");
    }

    [Fact]
    public void AIOptimizationRunResponse_ForRunningRun_DoesNotExposeStoredDiagnostic()
    {
        var optimization = new AIOptimizationRun
        {
            AlgorithmType = "Genetic",
            Status = "Running",
            ErrorMessage = "raw optimizer stack trace",
            CreatedAt = DateTime.UtcNow
        };

        var response = AIOptimizationRunResponse.FromEntity(optimization);

        response.ErrorMessage.Should().BeNull();
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
    public async Task GetAllDigitalTwins_WithExcessiveTake_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllDigitalTwins(take: 101);

        result.Should().BeOfType<BadRequestObjectResult>();
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
    public async Task GetPredictions_WithTooManyScenarioParameters_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));

        var result = await controller.GetPredictions(1, new PredictionRequest
        {
            ScenarioParameters = CreateOversizedDoubleDictionary()
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("ScenarioParameters");
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
            ModelDataJson = "{\"secret\":\"model-data\",\"Thrust\":100}",
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
        response.ModelDataJson.Should().BeNull();

        var responseJson = JsonSerializer.Serialize(response);
        responseJson.Should().NotContain("model-data");
        responseJson.Should().NotContain("engine-owner");
        responseJson.Should().NotContain(nameof(Engine.Thrust));
    }

    [Fact]
    public async Task UpdateDigitalTwinLearning_ReturnsSafeDigitalTwinResponseInsteadOfRawEntity()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("admin");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();
        var digitalTwinEngine = new DigitalTwinEngine();
        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            digitalTwinEngine);

        var createResult = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Learning twin"
        });
        var createdTwin = createResult.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        var result = await controller.UpdateDigitalTwinLearning(createdTwin.Id, new LearningDataRequest
        {
            TelemetryData = new Dictionary<string, double>
            {
                ["ChamberPressure"] = 1.0,
                ["Thrust"] = 1.0
            }
        });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeOfType<DigitalTwin>();
        var response = okResult.Value.Should().BeOfType<DigitalTwinResponse>().Subject;
        response.Engine.Should().NotBeNull();
        response.Engine!.Name.Should().Be(engine.Name);
        response.ModelDataJson.Should().BeNull();

        var responseJson = JsonSerializer.Serialize(response);
        responseJson.Should().NotContain(nameof(Engine.Thrust));
        responseJson.Should().NotContain(nameof(Engine.SpecificImpulse));
    }

    [Fact]
    public async Task UpdateDigitalTwinLearning_WithTooManyTelemetryFields_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateDigitalTwinController(context, CreatePrincipal("admin", isAdmin: true));

        var result = await controller.UpdateDigitalTwinLearning(1, new LearningDataRequest
        {
            TelemetryData = CreateOversizedDoubleDictionary()
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("TelemetryData");
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
    public async Task GetAllLaunches_WithExcessiveTake_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateLaunchesController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllLaunches(take: 101);

        result.Should().BeOfType<BadRequestObjectResult>();
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

    [Fact]
    public void LaunchResponse_ForNonFailedStatus_DoesNotExposeStoredDiagnostic()
    {
        var launch = new Launch
        {
            MissionName = "Leaky Progress",
            Status = "InProgress",
            ErrorMessage = "postgres timeout on internal host",
            ScheduledAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var response = LaunchResponse.FromEntity(launch);

        response.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ScheduleLaunch_ReturnsSafeLaunchResponseInsteadOfRawEntity()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("admin");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();
        var controller = CreateLaunchesController(context, CreatePrincipal("admin", isAdmin: true));

        var result = await controller.ScheduleLaunch(new ScheduleLaunchRequest
        {
            EngineId = engine.Id,
            MissionName = "Safe Mission"
        });

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = createdResult.Value.Should().BeOfType<LaunchResponse>().Subject;
        response.Engine.Should().NotBeNull();
        response.Engine!.Name.Should().Be(engine.Name);

        var responseJson = JsonSerializer.Serialize(response);
        responseJson.Should().NotContain(nameof(Engine.Thrust));
        responseJson.Should().NotContain(nameof(Engine.SpecificImpulse));
    }

    [Fact]
    public async Task ScheduleLaunch_WithTooManyLaunchParameters_ReturnsBadRequestWithoutCreatingLaunch()
    {
        await using var context = CreateContext();
        var controller = CreateLaunchesController(context, CreatePrincipal("admin", isAdmin: true));

        var result = await controller.ScheduleLaunch(new ScheduleLaunchRequest
        {
            EngineId = 1,
            MissionName = "Oversized Mission",
            LaunchParameters = CreateOversizedObjectDictionary()
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("LaunchParameters");
        context.Launches.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteLaunch_WhenBackgroundQueueIsFull_ReturnsServiceUnavailableWithoutStartingLaunch()
    {
        await using var context = CreateContext();
        var launch = await SeedLaunchAsync(context, "admin");
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            new RejectingBackgroundWorkQueue());

        var result = await controller.ExecuteLaunch(launch.Id);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        launch.Status.Should().Be("Scheduled");
        launch.LaunchedAt.Should().BeNull();
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
        ClaimsPrincipal user,
        IBackgroundWorkQueue? backgroundWorkQueue = null)
    {
        return new AIOptimizationController(
            context,
            new AdvancedAIOptimizationEngine(),
            NullLogger<AIOptimizationController>.Instance,
            backgroundWorkQueue ?? new RejectingBackgroundWorkQueue())
        {
            ControllerContext = CreateControllerContext(user)
        };
    }

    private static DigitalTwinController CreateDigitalTwinController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user,
        DigitalTwinEngine? digitalTwinEngine = null)
    {
        return new DigitalTwinController(
            context,
            digitalTwinEngine ?? new DigitalTwinEngine(),
            NullLogger<DigitalTwinController>.Instance)
        {
            ControllerContext = CreateControllerContext(user)
        };
    }

    private static LaunchesController CreateLaunchesController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user,
        IBackgroundWorkQueue? backgroundWorkQueue = null)
    {
        return new LaunchesController(
            context,
            new HelloblueGKEngine(),
            NullLogger<LaunchesController>.Instance,
            backgroundWorkQueue ?? new RejectingBackgroundWorkQueue())
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

    private static Dictionary<string, object> CreateOversizedObjectDictionary()
    {
        return Enumerable.Range(0, RequestPayloadLimits.MaxDictionaryEntries + 1)
            .ToDictionary(index => $"parameter-{index}", index => (object)index);
    }

    private static Dictionary<string, double> CreateOversizedDoubleDictionary()
    {
        return Enumerable.Range(0, RequestPayloadLimits.MaxDictionaryEntries + 1)
            .ToDictionary(index => $"parameter-{index}", index => (double)index);
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

    private sealed class RejectingBackgroundWorkQueue : IBackgroundWorkQueue
    {
        public int MaxConcurrency => 0;

        public bool TryAcquire(out BackgroundWorkSlot? slot)
        {
            slot = null;
            return false;
        }
    }
}
