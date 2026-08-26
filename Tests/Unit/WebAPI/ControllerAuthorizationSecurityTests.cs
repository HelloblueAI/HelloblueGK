using System.Security.Claims;
using System.Text.Json;
using HB_NLP_Research_Lab.AI;
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
    public async Task GetOptimizationStatus_ForDifferentStandardUser_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var optimization = await SeedOptimizationAsync(context, "alice");

        var controller = CreateOptimizationController(context, CreatePrincipal("bob"));

        var result = await controller.GetOptimizationStatus(optimization.Id);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task StartOptimization_ForEngineOwnedByDifferentUser_ReturnsNotFoundWithoutCreatingRun()
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

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { message = $"Engine with ID {engine.Id} not found" });
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
    public async Task GetPredictions_ForDifferentStandardUser_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "alice");

        var controller = CreateDigitalTwinController(context, CreatePrincipal("bob"));

        var result = await controller.GetPredictions(digitalTwin.Id, new PredictionRequest());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPredictions_AfterRuntimeRestart_RestoresPersistedTwin()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "alice");
        using var restartedEngine = new DigitalTwinEngine();
        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("alice"),
            restartedEngine);

        var result = await controller.GetPredictions(digitalTwin.Id, new PredictionRequest
        {
            ScenarioName = "Post-deployment prediction"
        });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseJson = JsonSerializer.Serialize(okResult.Value);
        responseJson.Should().Contain($"\"digitalTwinId\":{digitalTwin.Id}");
        responseJson.Should().Contain("\"predictions\"");
    }

    [Fact]
    public async Task GetPredictions_IgnoresClientEfficiencyOverride()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "alice");
        digitalTwin.Engine!.Efficiency = 0.88;
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));

        var result = await controller.GetPredictions(digitalTwin.Id, new PredictionRequest
        {
            ScenarioName = "Efficiency forge",
            ScenarioParameters = new Dictionary<string, double>
            {
                ["efficiency"] = 0.99
            }
        });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(okResult.Value));
        var predictedMetrics = document.RootElement
            .GetProperty("predictions")
            .GetProperty("PredictedMetrics");
        predictedMetrics.GetProperty("Efficiency").GetDouble().Should().BeApproximately(0.88, 0.0001);
    }

    [Fact]
    public async Task UpdateDigitalTwinLearning_AfterRuntimeRestart_RestoresPersistedTwin()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "admin");
        using var restartedEngine = new DigitalTwinEngine();
        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            restartedEngine);

        var result = await controller.UpdateDigitalTwinLearning(digitalTwin.Id, new LearningDataRequest
        {
            TelemetryData = new Dictionary<string, double>
            {
                ["Thrust"] = 1.0
            }
        });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DigitalTwinResponse>().Subject;
        response.TrainingIterations.Should().Be(1);
        response.ModelDataJson.Should().BeNull();
    }

    [Fact]
    public async Task CreateDigitalTwin_ForEngineOwnedByDifferentUser_ReturnsNotFoundWithoutCreatingTwin()
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

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { message = $"Engine with ID {engine.Id} not found" });
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
    public async Task GetLaunchById_ForDifferentStandardUser_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var launch = await SeedLaunchAsync(context, "alice");

        var controller = CreateLaunchesController(context, CreatePrincipal("bob"));

        var result = await controller.GetLaunchById(launch.Id);

        result.Should().BeOfType<NotFoundObjectResult>();
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
    public async Task ScheduleLaunch_WithWhitespaceMissionName_ReturnsBadRequestWithoutCreatingLaunch()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("admin");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();
        var controller = CreateLaunchesController(context, CreatePrincipal("admin", isAdmin: true));

        var result = await controller.ScheduleLaunch(new ScheduleLaunchRequest
        {
            EngineId = engine.Id,
            MissionName = "   "
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.Launches.Should().BeEmpty();
    }

    [Fact]
    public async Task ScheduleLaunch_WithNonFiniteLaunchParameter_ReturnsBadRequestWithoutCreatingLaunch()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("admin");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();
        var controller = CreateLaunchesController(context, CreatePrincipal("admin", isAdmin: true));

        var result = await controller.ScheduleLaunch(new ScheduleLaunchRequest
        {
            EngineId = engine.Id,
            MissionName = "Infinity Burn",
            LaunchParameters = new Dictionary<string, object>
            {
                ["massRatio"] = double.PositiveInfinity
            }
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("finite");
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

    [Fact]
    public async Task ExecuteLaunch_ConcurrentCalls_OnlyOneTransitionsAndQueuesWork()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");

        await using var contextA = CreateContext(databaseName);
        await using var contextB = CreateContext(databaseName);
        var queueA = new DeferredBackgroundWorkQueue();
        var queueB = new DeferredBackgroundWorkQueue();
        var controllerA = CreateLaunchesController(
            contextA,
            CreatePrincipal("admin", isAdmin: true),
            queueA);
        var controllerB = CreateLaunchesController(
            contextB,
            CreatePrincipal("admin", isAdmin: true),
            queueB);

        var results = await Task.WhenAll(
            controllerA.ExecuteLaunch(launch.Id),
            controllerB.ExecuteLaunch(launch.Id));

        results.Count(result => result is OkObjectResult).Should().Be(1);
        results.Count(result => result is BadRequestObjectResult).Should().Be(1);
        (queueA.PendingWork.Count + queueB.PendingWork.Count).Should().Be(1);

        var persisted = await context.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("InProgress");
        persisted.LaunchedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelLaunch_ForInProgressLaunch_IsNotOverwrittenByBackgroundWorkerCompletion()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var executeController = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var executeResult = await executeController.ExecuteLaunch(launch.Id);
        executeResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var cancelContext = CreateContext(databaseName);
        var cancelController = CreateLaunchesController(
            cancelContext,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);
        var cancelResult = await cancelController.CancelLaunch(launch.Id);
        cancelResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.CancelledWorkItems.Should().ContainSingle()
            .Which.Should().Be($"launch:{launch.Id}");

        await using var workerContext = CreateContext(databaseName);
        var serviceProvider = new SingleServiceProvider(workerContext);
        await deferredQueue.PendingWork[0].Work(serviceProvider, CancellationToken.None);

        var persisted = await context.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Cancelled");
        persisted.CompletedAt.Should().NotBeNull();
        persisted.MissionSuccess.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteLaunch_WhenWorkerTokenCancelled_PersistsCancelledWithoutMissionFailure()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var executeResult = await controller.ExecuteLaunch(launch.Id);
        executeResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), cts.Token);

        var persisted = await workerContext.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Cancelled");
        persisted.MissionSuccess.Should().BeNull();
        persisted.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteLaunch_AppliesStoredLaunchParametersToMissionResults()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");
        launch.LaunchParametersJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["burnTimeSeconds"] = 90.0,
            ["massRatio"] = 3.0,
            ["successEfficiencyThreshold"] = 0.5,
            ["successAccuracyThreshold"] = 1.0
        });
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var executeResult = await controller.ExecuteLaunch(launch.Id);
        executeResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var persisted = await workerContext.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().BeOneOf("Success", "Failed");
        persisted.ResultsJson.Should().NotBeNullOrWhiteSpace();

        using var document = JsonDocument.Parse(persisted.ResultsJson!);
        document.RootElement.GetProperty("burnTime").GetDouble().Should().BeApproximately(90.0, 0.0001);
        document.RootElement.GetProperty("massRatio").GetDouble().Should().BeApproximately(3.0, 0.0001);
        document.RootElement.GetProperty("appliedLaunchParameters").ValueKind.Should().Be(JsonValueKind.Object);
        document.RootElement.GetProperty("deltaV").GetDouble().Should().BeGreaterThan(
            launch.Engine.SpecificImpulse * 9.81 * Math.Log(2.0));
    }

    [Fact]
    public async Task ExecuteLaunch_IgnoresClientSuccessThresholdsForMissionPassFail()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");
        // Engine efficiency is below the server floor (0.90). Ultra-low client thresholds
        // previously forged MissionSuccess=true; they must be ignored.
        launch.Engine.Efficiency = 0.50;
        launch.LaunchParametersJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["successEfficiencyThreshold"] = 0.001,
            ["successAccuracyThreshold"] = 0.001
        });
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var executeResult = await controller.ExecuteLaunch(launch.Id);
        executeResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var persisted = await workerContext.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Failed");
        persisted.MissionSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteLaunch_IgnoresClientEfficiencyOverrideForMissionPassFail()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");
        // Persisted engine is below the server efficiency floor. Client efficiency override
        // must not forge MissionSuccess — scenario physics may still use the override.
        launch.Engine.Efficiency = 0.50;
        launch.LaunchParametersJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["efficiency"] = 0.95
        });
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var executeResult = await controller.ExecuteLaunch(launch.Id);
        executeResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var persisted = await workerContext.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Failed");
        persisted.MissionSuccess.Should().BeFalse();
        persisted.ResultsJson.Should().NotBeNullOrWhiteSpace();

        using var document = JsonDocument.Parse(persisted.ResultsJson!);
        document.RootElement.GetProperty("engineEfficiency").GetDouble().Should().BeApproximately(0.50, 0.0001);
        document.RootElement.GetProperty("simulatedEfficiency").GetDouble().Should().BeApproximately(0.95, 0.0001);
    }

    [Fact]
    public async Task ExecuteLaunch_FailsMissionSuccessWhenValidationUnproven_EvenWithHighEfficiency()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");
        // Engine clears the efficiency floor; without trusted validation evidence the
        // accuracy gate must fail closed (solver Accuracy must not stand in for validation).
        launch.Engine.Efficiency = 0.95;
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var executeResult = await controller.ExecuteLaunch(launch.Id);
        executeResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var persisted = await workerContext.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Failed");
        persisted.MissionSuccess.Should().BeFalse();
        persisted.ResultsJson.Should().NotBeNullOrWhiteSpace();

        using var document = JsonDocument.Parse(persisted.ResultsJson!);
        document.RootElement.GetProperty("validationAccuracy").GetDouble()
            .Should().Be(RealTimeValidationEngine.UnprovenValidationAccuracy);
    }

    [Fact]
    public async Task ExecuteLaunch_WhenEngineDeactivated_ReturnsBadRequestWithoutStarting()
    {
        await using var context = CreateContext();
        var launch = await SeedLaunchAsync(context, "admin");
        launch.Engine.IsActive = false;
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            deferredQueue);

        var result = await controller.ExecuteLaunch(launch.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        deferredQueue.PendingWork.Should().BeEmpty();
        var persisted = await context.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Scheduled");
        persisted.LaunchedAt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteLaunch_WhenQueueThrowsAfterClaim_MarksLaunchFailed()
    {
        await using var context = CreateContext();
        var launch = await SeedLaunchAsync(context, "admin");
        var controller = CreateLaunchesController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            new ThrowingBackgroundWorkQueue());

        var result = await controller.ExecuteLaunch(launch.Id);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
        var persisted = await context.Launches.AsNoTracking().SingleAsync(l => l.Id == launch.Id);
        persisted.Status.Should().Be("Failed");
        persisted.MissionSuccess.Should().BeFalse();
        persisted.ErrorMessage.Should().Contain("could not be queued");
    }

    [Fact]
    public async Task StartOptimization_UsesRequestedAlgorithmAndParameterOverrides()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = CreateEngine("alice");
        engine.CreatedBy = null;
        engine.Thrust = 1_000_000;
        engine.SpecificImpulse = 300;
        engine.ChamberPressure = 10_000_000;
        engine.Efficiency = 0.8;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            deferredQueue);

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic",
            Parameters = new Dictionary<string, object> { ["efficiency"] = 0.88 }
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<AIOptimizationRunResponse>().Subject;
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var persisted = await workerContext.AIOptimizationRuns.AsNoTracking()
            .SingleAsync(o => o.Id == response.Id);
        persisted.Status.Should().Be("Completed");
        persisted.ResultsJson.Should().NotBeNullOrWhiteSpace();

        using var document = JsonDocument.Parse(persisted.ResultsJson!);
        document.RootElement.GetProperty("algorithmType").GetString().Should().Be("Genetic");
        // Improvement baseline is persisted Engine.Efficiency — not the client scenario override.
        document.RootElement.GetProperty("originalParameters").GetProperty("efficiency").GetDouble()
            .Should().BeApproximately(0.8, 0.0001);
        document.RootElement.GetProperty("appliedParameters").GetProperty("efficiency").GetDouble()
            .Should().BeApproximately(0.88, 0.0001);
        document.RootElement.GetProperty("stages").EnumerateArray()
            .Select(element => element.GetString())
            .Should().Equal("Genetic Algorithm");
    }

    [Fact]
    public async Task StartOptimization_ImprovementPercentage_UsesPersistedEngineBaseline()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = CreateEngine("alice");
        engine.CreatedBy = null;
        engine.Thrust = 1_000_000;
        engine.SpecificImpulse = 300;
        engine.ChamberPressure = 10_000_000;
        engine.Efficiency = 0.8;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            deferredQueue);

        // A tiny client efficiency override used to inflate ImprovementPercentage
        // against the overridden baseline instead of the persisted engine.
        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic",
            Parameters = new Dictionary<string, object> { ["efficiency"] = 0.001 }
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<AIOptimizationRunResponse>().Subject;
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var persisted = await workerContext.AIOptimizationRuns.AsNoTracking()
            .SingleAsync(o => o.Id == response.Id);
        persisted.Status.Should().Be("Completed");

        using var document = JsonDocument.Parse(persisted.ResultsJson!);
        document.RootElement.GetProperty("baselineEfficiency").GetDouble()
            .Should().BeApproximately(0.8, 0.0001);
        document.RootElement.GetProperty("originalParameters").GetProperty("efficiency").GetDouble()
            .Should().BeApproximately(0.001, 0.0001);

        var optimizedEfficiency = document.RootElement.GetProperty("optimizedParameters")
            .GetProperty("efficiency").GetDouble();
        var expectedImprovement = ((optimizedEfficiency - 0.8) / 0.8) * 100;
        persisted.ImprovementPercentage.Should().BeApproximately(expectedImprovement, 0.0001);
        // Client 0.001 → 0.95 would be a ~95,000% forge if the override were the baseline.
        persisted.ImprovementPercentage.Should().BeLessThan(100);
    }

    [Fact]
    public async Task StartOptimization_WhenEngineIsDeletedBeforeWorkerRuns_MarksRunFailed()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            deferredQueue);

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic"
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<AIOptimizationRunResponse>().Subject;
        deferredQueue.PendingWork.Should().ContainSingle();

        // Simulate a missing engine row without cascading the optimization run away.
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM Engines WHERE Id = {engine.Id}");
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        await using var workerContext = CreateContext(databaseName);
        var serviceProvider = new SingleServiceProvider(workerContext);
        await deferredQueue.PendingWork[0].Work(serviceProvider, CancellationToken.None);

        var persisted = await context.AIOptimizationRuns.AsNoTracking()
            .SingleAsync(o => o.Id == response.Id);
        persisted.Status.Should().Be("Failed");
        persisted.CompletedAt.Should().NotBeNull();
        persisted.ErrorMessage.Should().Contain("engine no longer exists");
    }

    [Fact]
    public async Task CreateDigitalTwin_WithForceCreate_DeactivatesExistingActiveTwins()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));
        var firstCreate = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Twin A"
        });
        var firstTwin = firstCreate.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        var secondCreate = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Twin B",
            ForceCreate = true
        });
        var secondTwin = secondCreate.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        var persistedFirst = await context.DigitalTwins.AsNoTracking().SingleAsync(dt => dt.Id == firstTwin.Id);
        var persistedSecond = await context.DigitalTwins.AsNoTracking().SingleAsync(dt => dt.Id == secondTwin.Id);
        persistedFirst.IsActive.Should().BeFalse();
        persistedSecond.IsActive.Should().BeTrue();
        context.DigitalTwins.Count(dt => dt.EngineId == engine.Id && dt.IsActive).Should().Be(1);
    }

    [Fact]
    public async Task CreateDigitalTwin_ForInactiveEngine_ReturnsBadRequestWithoutCreatingRuntime()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("alice");
        engine.IsActive = false;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        using var digitalTwinEngine = new DigitalTwinEngine();
        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("alice"),
            digitalTwinEngine);

        var result = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Inactive Twin"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.DigitalTwins.Should().BeEmpty();

        var ownerKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("alice")))[..16];
        var runtimeKey = $"Owner_{ownerKey}_Engine_{engine.Id}";
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeFalse();
    }

    [Fact]
    public async Task CreateDigitalTwin_WhenNonUniqueSaveFails_RemovesRuntimeState()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var seedContext = CreateContext(databaseName);
        var engine = CreateEngine("alice");
        seedContext.Engines.Add(engine);
        await seedContext.SaveChangesAsync();

        await using var failingContext = CreateFailingDigitalTwinSaveContext(databaseName);
        using var digitalTwinEngine = new DigitalTwinEngine();
        var controller = CreateDigitalTwinController(
            failingContext,
            CreatePrincipal("alice"),
            digitalTwinEngine);

        var result = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Orphan Twin"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        (await seedContext.DigitalTwins.AsNoTracking().CountAsync()).Should().Be(0);

        var ownerKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("alice")))[..16];
        var runtimeKey = $"Owner_{ownerKey}_Engine_{engine.Id}";
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeFalse();
    }

    [Fact]
    public async Task CreateDigitalTwin_ForceCreate_WhenSaveFails_RestoresPriorRuntime()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        using var digitalTwinEngine = new DigitalTwinEngine();
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("alice"),
            digitalTwinEngine);
        var firstCreate = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Twin A"
        });
        firstCreate.Should().BeOfType<CreatedAtActionResult>();

        await using var failingContext = CreateFailingDigitalTwinSaveContext(databaseName);
        var failingController = CreateDigitalTwinController(
            failingContext,
            CreatePrincipal("alice"),
            digitalTwinEngine);

        var result = await failingController.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Twin B",
            ForceCreate = true
        });

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        (await context.DigitalTwins.AsNoTracking().CountAsync(dt => dt.IsActive)).Should().Be(1);

        var ownerKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("alice")))[..16];
        var runtimeKey = $"Owner_{ownerKey}_Engine_{engine.Id}";

        // Prior active twin remains persisted; runtime must be restored for it.
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeTrue();
    }

    [Fact]
    public async Task GetOptimizationById_ForCaseVariantUsername_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var optimization = await SeedOptimizationAsync(context, "alice");

        // Case-variant account must not inherit ordinal ownership via IgnoreCase compares.
        var controller = CreateOptimizationController(context, CreatePrincipal("Alice"));

        var result = await controller.GetOptimizationById(optimization.Id);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task StartOptimization_WhenEngineDeactivatedBeforeWorkerRuns_MarksRunFailed()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            deferredQueue);

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic"
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<AIOptimizationRunResponse>().Subject;
        deferredQueue.PendingWork.Should().ContainSingle();

        engine.IsActive = false;
        await context.SaveChangesAsync();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(
            new SingleServiceProvider(workerContext),
            CancellationToken.None);

        var persisted = await context.AIOptimizationRuns.AsNoTracking()
            .SingleAsync(o => o.Id == response.Id);
        persisted.Status.Should().Be("Failed");
        persisted.ErrorMessage.Should().Contain("inactive");
    }

    [Fact]
    public async Task CancelOptimization_ForPendingRun_MarksCancelledAndPreventsCompletion()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var startController = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            deferredQueue);

        var startResult = await startController.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic"
        });

        var created = startResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<AIOptimizationRunResponse>().Subject;

        var cancelController = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            deferredQueue);
        var cancelResult = await cancelController.CancelOptimization(response.Id);
        cancelResult.Should().BeOfType<OkObjectResult>();
        deferredQueue.CancelledWorkItems.Should().ContainSingle()
            .Which.Should().Be($"optimization:{response.Id}");

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(
            new SingleServiceProvider(workerContext),
            CancellationToken.None);

        var persisted = await context.AIOptimizationRuns.AsNoTracking()
            .SingleAsync(o => o.Id == response.Id);
        persisted.Status.Should().Be("Cancelled");
        persisted.ResultsJson.Should().BeNull();
    }

    [Fact]
    public async Task CreateDigitalTwin_CaseVariantOwners_UseDistinctRuntimeKeys()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("catalog");
        engine.CreatedBy = null; // shared catalog engine
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        // Seed two case-variant owners each with an active twin row for the shared engine.
        context.DigitalTwins.Add(new DigitalTwin
        {
            EngineId = engine.Id,
            Name = "alice-twin",
            CreatedBy = "alice",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModelDataJson = "{}"
        });
        context.DigitalTwins.Add(new DigitalTwin
        {
            EngineId = engine.Id,
            Name = "Alice-twin",
            CreatedBy = "Alice",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ModelDataJson = "{}"
        });
        await context.SaveChangesAsync();

        using var digitalTwinEngine = new DigitalTwinEngine();
        var aliceController = CreateDigitalTwinController(
            context,
            CreatePrincipal("alice"),
            digitalTwinEngine);
        var aliceCaseController = CreateDigitalTwinController(
            context,
            CreatePrincipal("Alice"),
            digitalTwinEngine);

        var aliceTwinId = await context.DigitalTwins
            .Where(dt => dt.CreatedBy == "alice")
            .Select(dt => dt.Id)
            .SingleAsync();
        var aliceCaseTwinId = await context.DigitalTwins
            .Where(dt => dt.CreatedBy == "Alice")
            .Select(dt => dt.Id)
            .SingleAsync();

        var alicePredict = await aliceController.GetPredictions(
            aliceTwinId,
            new PredictionRequest { ScenarioName = "baseline" });
        var aliceCasePredict = await aliceCaseController.GetPredictions(
            aliceCaseTwinId,
            new PredictionRequest { ScenarioName = "baseline" });

        alicePredict.Should().BeOfType<OkObjectResult>();
        aliceCasePredict.Should().BeOfType<OkObjectResult>();

        var aliceKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("alice")))[..16];
        var aliceCaseKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("Alice")))[..16];
        aliceKey.Should().NotBe(aliceCaseKey);

        digitalTwinEngine.RemoveDigitalTwin($"Owner_{aliceKey}_Engine_{engine.Id}").Should().BeTrue();
        digitalTwinEngine.RemoveDigitalTwin($"Owner_{aliceCaseKey}_Engine_{engine.Id}").Should().BeTrue();
    }

    [Fact]
    public async Task StartOptimization_ForInactiveEngine_ReturnsBadRequestWithoutCreatingRun()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("alice");
        engine.IsActive = false;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateOptimizationController(
            context,
            CreatePrincipal("alice"),
            new DeferredBackgroundWorkQueue());

        var result = await controller.StartOptimization(new StartOptimizationRequest
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.AIOptimizationRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDigitalTwin_UniqueConstraintRace_KeepsWinnerRuntimeState()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var contextA = CreateContext(databaseName);
        await using var contextB = CreateContext(databaseName);

        var engine = CreateEngine("alice");
        contextA.Engines.Add(engine);
        await contextA.SaveChangesAsync();

        using var digitalTwinEngine = new DigitalTwinEngine();
        var controllerA = CreateDigitalTwinController(
            contextA,
            CreatePrincipal("alice"),
            digitalTwinEngine);
        var controllerB = CreateDigitalTwinController(
            contextB,
            CreatePrincipal("alice"),
            digitalTwinEngine);

        var request = new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Raced Twin"
        };

        var results = await Task.WhenAll(
            controllerA.CreateDigitalTwin(request),
            controllerB.CreateDigitalTwin(request));

        results.Should().ContainSingle(result => result is CreatedAtActionResult);
        results.Should().ContainSingle(result => result is ConflictObjectResult);
        (await contextA.DigitalTwins.AsNoTracking()
            .CountAsync(dt => dt.EngineId == engine.Id && dt.IsActive && dt.CreatedBy == "alice"))
            .Should().Be(1);

        var ownerKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("alice")))[..16];
        var runtimeKey = $"Owner_{ownerKey}_Engine_{engine.Id}";

        // Loser must not wipe the shared runtime state the winner still needs.
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeTrue();
    }

    [Fact]
    public async Task CreateDigitalTwin_AdminForceCreate_DoesNotDeactivateOtherUsersTwins()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("shared");
        engine.CreatedBy = null;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var aliceController = CreateDigitalTwinController(context, CreatePrincipal("alice"));
        var aliceCreate = await aliceController.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Alice Twin"
        });
        var aliceTwin = aliceCreate.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        var adminController = CreateDigitalTwinController(context, CreatePrincipal("admin", isAdmin: true));
        var adminCreate = await adminController.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Admin Twin",
            ForceCreate = true
        });
        adminCreate.Should().BeOfType<CreatedAtActionResult>();

        var persistedAlice = await context.DigitalTwins.AsNoTracking().SingleAsync(dt => dt.Id == aliceTwin.Id);
        persistedAlice.IsActive.Should().BeTrue();
        context.DigitalTwins.Count(dt => dt.EngineId == engine.Id && dt.IsActive).Should().Be(2);
    }

    [Fact]
    public async Task DeactivateDigitalTwin_RemovesRuntimeTwinState()
    {
        await using var context = CreateContext();
        using var digitalTwinEngine = new DigitalTwinEngine();
        var engine = CreateEngine("admin");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("admin", isAdmin: true),
            digitalTwinEngine);

        var createResult = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Runtime Twin"
        });
        var twin = createResult.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        // Confirm runtime state exists, then deactivate and confirm eviction.
        var ownerKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("admin")))[..16];
        var runtimeKey = $"Owner_{ownerKey}_Engine_{engine.Id}";
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeTrue();

        // Recreate runtime state through the controller path again.
        await digitalTwinEngine.CreateDigitalTwinAsync(
            runtimeKey,
            new EngineModel { Name = engine.Name, Parameters = new Dictionary<string, double>() });

        var deactivate = await controller.DeactivateDigitalTwin(twin.Id);
        deactivate.Should().BeOfType<OkObjectResult>();
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateDigitalTwin_InactiveHistoricalTwin_DoesNotEvictActiveRuntime()
    {
        await using var context = CreateContext();
        using var digitalTwinEngine = new DigitalTwinEngine();
        var engine = CreateEngine("alice");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(
            context,
            CreatePrincipal("alice", isAdmin: true),
            digitalTwinEngine);

        var firstCreate = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Twin A"
        });
        var firstTwin = firstCreate.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        var forceCreate = await controller.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Twin B",
            ForceCreate = true
        });
        forceCreate.Should().BeOfType<CreatedAtActionResult>();

        var ownerKey = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("alice")))[..16];
        var runtimeKey = $"Owner_{ownerKey}_Engine_{engine.Id}";

        // Seed learning state on the active twin runtime.
        await digitalTwinEngine.LearnFromTestFlightAsync(
            runtimeKey,
            new TestFlightData
            {
                EngineId = runtimeKey,
                FlightDate = DateTime.UtcNow,
                FlightMetrics = new Dictionary<string, double> { ["Thrust"] = 1_000_000 }
            });

        // Deactivating the historical inactive twin must not wipe the shared runtime key.
        var deactivateHistorical = await controller.DeactivateDigitalTwin(firstTwin.Id);
        deactivateHistorical.Should().BeOfType<OkObjectResult>();

        var report = await digitalTwinEngine.GenerateLearningPerformanceReportAsync(runtimeKey);
        report.TotalLearningEvents.Should().Be(1);
        digitalTwinEngine.RemoveDigitalTwin(runtimeKey).Should().BeTrue();
    }

    [Fact]
    public async Task GetPredictions_ForInactiveDigitalTwin_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "alice");
        digitalTwin.IsActive = false;
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));
        var result = await controller.GetPredictions(digitalTwin.Id, new PredictionRequest
        {
            ScenarioName = "Inactive prediction"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateDigitalTwinLearning_ForInactiveDigitalTwin_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "admin");
        digitalTwin.IsActive = false;
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("admin", isAdmin: true));
        var result = await controller.UpdateDigitalTwinLearning(digitalTwin.Id, new LearningDataRequest
        {
            TelemetryData = new Dictionary<string, double> { ["Thrust"] = 1.0 }
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPredictions_ForInactiveEngine_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "alice");
        digitalTwin.Engine!.IsActive = false;
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("alice"));
        var result = await controller.GetPredictions(digitalTwin.Id, new PredictionRequest
        {
            ScenarioName = "Inactive engine prediction"
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("engine is inactive");
    }

    [Fact]
    public async Task UpdateDigitalTwinLearning_ForInactiveEngine_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var digitalTwin = await SeedDigitalTwinAsync(context, "admin");
        digitalTwin.Engine!.IsActive = false;
        await context.SaveChangesAsync();

        var controller = CreateDigitalTwinController(context, CreatePrincipal("admin", isAdmin: true));
        var result = await controller.UpdateDigitalTwinLearning(digitalTwin.Id, new LearningDataRequest
        {
            TelemetryData = new Dictionary<string, double> { ["Thrust"] = 1.0 }
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("engine is inactive");
    }

    private static HelloblueGKDbContext CreateContext(string? databaseName = null)
    {
        // Use SQLite instead of the EF InMemory provider so ExecuteUpdate-based
        // conditional status transitions can be exercised in tests.
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite($"Data Source=file:{(databaseName ?? Guid.NewGuid().ToString("N"))}?mode=memory&cache=shared")
            .Options;

        var context = new HelloblueGKDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static FailingDigitalTwinSaveDbContext CreateFailingDigitalTwinSaveContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite($"Data Source=file:{databaseName}?mode=memory&cache=shared")
            .Options;

        var context = new FailingDigitalTwinSaveDbContext(options);
        context.Database.OpenConnection();
        return context;
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
            IsActive = true,
            RealTimeLearning = true
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

        public bool TryAcquire(out IBackgroundWorkSlot? slot)
        {
            slot = null;
            return false;
        }

        public bool TryCancel(string workItemName) => false;
    }

    private sealed class ThrowingBackgroundWorkQueue : IBackgroundWorkQueue
    {
        public int MaxConcurrency => 1;

        public bool TryAcquire(out IBackgroundWorkSlot? slot)
        {
            slot = new ThrowingBackgroundWorkSlot();
            return true;
        }

        public bool TryCancel(string workItemName) => false;
    }

    private sealed class ThrowingBackgroundWorkSlot : IBackgroundWorkSlot
    {
        public void Queue(
            Func<IServiceProvider, CancellationToken, Task> workItem,
            string workItemName)
        {
            throw new InvalidOperationException("Simulated queue failure");
        }

        public void Dispose()
        {
        }
    }

    private sealed class DeferredBackgroundWorkQueue : IBackgroundWorkQueue
    {
        public int MaxConcurrency => 1;
        public List<(Func<IServiceProvider, CancellationToken, Task> Work, string Name)> PendingWork { get; } = new();
        public List<string> CancelledWorkItems { get; } = new();

        public bool TryAcquire(out IBackgroundWorkSlot? slot)
        {
            slot = new DeferredBackgroundWorkSlot(this);
            return true;
        }

        public bool TryCancel(string workItemName)
        {
            CancelledWorkItems.Add(workItemName);
            return true;
        }
    }

    private sealed class DeferredBackgroundWorkSlot : IBackgroundWorkSlot
    {
        private readonly DeferredBackgroundWorkQueue _owner;
        private int _state;

        public DeferredBackgroundWorkSlot(DeferredBackgroundWorkQueue owner)
        {
            _owner = owner;
        }

        public void Queue(
            Func<IServiceProvider, CancellationToken, Task> workItem,
            string workItemName)
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                throw new InvalidOperationException("Background work slot has already been used.");
            }

            _owner.PendingWork.Add((workItem, workItemName));
        }

        public void Dispose()
        {
            Interlocked.CompareExchange(ref _state, 1, 0);
        }
    }

    private sealed class SingleServiceProvider : IServiceProvider
    {
        private readonly HelloblueGKDbContext _context;

        public SingleServiceProvider(HelloblueGKDbContext context)
        {
            _context = context;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(HelloblueGKDbContext))
            {
                return _context;
            }

            return null;
        }
    }

    private sealed class FailingDigitalTwinSaveDbContext : HelloblueGKDbContext
    {
        public FailingDigitalTwinSaveDbContext(DbContextOptions<HelloblueGKDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<DigitalTwin>().Any(entry => entry.State == EntityState.Added))
            {
                throw new InvalidOperationException("Forced digital twin save failure for orphan cleanup test.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

}
