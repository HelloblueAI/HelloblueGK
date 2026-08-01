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

public class SimulationsControllerSecurityTests
{
    [Fact]
    public async Task GetAllSimulations_ForStandardUser_ReturnsOnlyOwnedSimulations()
    {
        await using var context = CreateContext();
        await SeedSimulationAsync(context, "alice", "Running");
        await SeedSimulationAsync(context, "bob", "Pending");

        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllSimulations();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var simulations = okResult.Value.Should()
            .BeAssignableTo<IEnumerable<EngineSimulationResponse>>()
            .Subject
            .ToList();

        simulations.Should().ContainSingle();
        simulations[0].CreatedBy.Should().Be("alice");
    }

    [Fact]
    public async Task GetAllSimulations_WithExcessiveTake_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllSimulations(take: 101);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetAllSimulations_WithPagination_ReturnsRequestedOwnedPage()
    {
        await using var context = CreateContext();
        await SeedSimulationAsync(context, "alice", "Pending");
        await SeedSimulationAsync(context, "alice", "Running");
        await SeedSimulationAsync(context, "alice", "Completed");
        await SeedSimulationAsync(context, "bob", "Pending");
        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.GetAllSimulations(skip: 1, take: 1);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var simulations = okResult.Value.Should()
            .BeAssignableTo<IEnumerable<EngineSimulationResponse>>()
            .Subject
            .ToList();
        simulations.Should().ContainSingle();
        simulations[0].CreatedBy.Should().Be("alice");
    }

    [Fact]
    public async Task CancelSimulation_ForDifferentStandardUser_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var simulation = await SeedSimulationAsync(context, "alice", "Running");

        var controller = CreateController(context, CreatePrincipal("bob"));

        var result = await controller.CancelSimulation(simulation.Id);

        result.Should().BeOfType<NotFoundObjectResult>();
        simulation.Status.Should().Be("Running");
    }

    [Fact]
    public async Task CancelSimulation_ForAdmin_AllowsCancellation()
    {
        await using var context = CreateContext();
        var simulation = await SeedSimulationAsync(context, "alice", "Running");

        var controller = CreateController(context, CreatePrincipal("admin", isAdmin: true));

        var result = await controller.CancelSimulation(simulation.Id);

        result.Should().BeOfType<OkObjectResult>();
        simulation.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelledSimulation_IsNotOverwrittenByBackgroundWorkerCompletion()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var engine = new Engine
        {
            Name = "Cancel-safe Engine",
            EngineType = "Test",
            CreatedBy = null,
            Thrust = 1,
            SpecificImpulse = 1,
            ChamberPressure = 1,
            Efficiency = 0.95,
            ExpansionRatio = 1,
            MassFlowRate = 1
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateController(context, CreatePrincipal("alice"), deferredQueue);

        var createResult = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "MultiPhysics"
        });

        var created = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<EngineSimulationResponse>().Subject;
        response.Status.Should().Be("Pending");
        deferredQueue.PendingWork.Should().ContainSingle();

        var cancelResult = await controller.CancelSimulation(response.Id);
        cancelResult.Should().BeOfType<OkObjectResult>();

        await using var workerContext = CreateContext(databaseName);
        var serviceProvider = new SingleServiceProvider(workerContext);
        await deferredQueue.PendingWork[0].Work(serviceProvider, CancellationToken.None);

        var simulation = await workerContext.EngineSimulations
            .AsNoTracking()
            .SingleAsync(s => s.Id == response.Id);
        simulation.Status.Should().Be("Cancelled");
        simulation.ResultsJson.Should().BeNull();
    }

    [Fact]
    public async Task RunSimulation_ForStandardUserWithoutUsernameClaim_ReturnsForbidWithoutCreatingOrphanRecord()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Shared Engine",
            EngineType = "Test",
            CreatedBy = "admin"
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();
        var controller = CreateController(context, CreatePrincipalWithoutUsername());

        var result = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "CFD"
        });

        result.Should().BeOfType<ForbidResult>();
        context.EngineSimulations.Should().BeEmpty();
    }

    [Fact]
    public async Task RunSimulation_ForEngineOwnedByDifferentUser_ReturnsNotFoundWithoutCreatingSimulation()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Alice Private Engine",
            EngineType = "Test",
            CreatedBy = "alice"
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateController(context, CreatePrincipal("bob"));

        var result = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "CFD"
        });

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { message = $"Engine with ID {engine.Id} not found" });
        context.EngineSimulations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSimulationById_ForDifferentStandardUser_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var simulation = await SeedSimulationAsync(context, "alice", "Completed");

        var controller = CreateController(context, CreatePrincipal("bob"));

        var result = await controller.GetSimulationById(simulation.Id);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().BeEquivalentTo(new { message = $"Simulation with ID {simulation.Id} not found" });
    }

    [Fact]
    public async Task RunSimulation_UsesRequestedSimulationTypeAndParametersInResults()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = new Engine
        {
            Name = "Typed Simulation Engine",
            EngineType = "Test",
            CreatedBy = null,
            Thrust = 1,
            SpecificImpulse = 1,
            ChamberPressure = 1,
            Efficiency = 0.9,
            ExpansionRatio = 1,
            MassFlowRate = 1
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateController(context, CreatePrincipal("alice"), deferredQueue);

        var createResult = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "Thermal",
            Parameters = new Dictionary<string, object>
            {
                ["iterations"] = 77,
                ["maxTemperature"] = 3900,
                ["coolingEfficiency"] = 0.82
            }
        });

        var created = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<EngineSimulationResponse>().Subject;
        deferredQueue.PendingWork.Should().ContainSingle();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(new SingleServiceProvider(workerContext), CancellationToken.None);

        var simulation = await workerContext.EngineSimulations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == response.Id);

        simulation.Status.Should().Be("Completed");
        simulation.Iterations.Should().Be(77);
        simulation.ResultsJson.Should().NotBeNullOrWhiteSpace();
        using var document = JsonDocument.Parse(simulation.ResultsJson!);
        document.RootElement.GetProperty("simulationType").GetString().Should().Be("Thermal");
        document.RootElement.GetProperty("parameters").GetProperty("iterations").GetInt32().Should().Be(77);
        document.RootElement.GetProperty("thermalAnalysis").GetProperty("maxTemperature").GetDouble()
            .Should().Be(3900);
        document.RootElement.GetProperty("thermalAnalysis").GetProperty("coolingEfficiency").GetDouble()
            .Should().BeApproximately(0.82, 0.0001);
        document.RootElement.GetProperty("thrustAnalysis").GetProperty("maxThrust").GetDouble()
            .Should().Be(0);
    }

    [Fact]
    public async Task RunSimulation_ForInactiveEngine_ReturnsBadRequestWithoutCreatingSimulation()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Inactive Engine",
            EngineType = "Test",
            CreatedBy = null,
            IsActive = false
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateController(context, CreatePrincipal("alice"), new DeferredBackgroundWorkQueue());

        var result = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "CFD"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.EngineSimulations.Should().BeEmpty();
    }

    [Fact]
    public async Task RunSimulation_WhenEngineDeactivatedBeforeWorkerRuns_MarksSimulationFailed()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var engine = new Engine
        {
            Name = "Shared Engine",
            EngineType = "Test",
            CreatedBy = null,
            IsActive = true
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var deferredQueue = new DeferredBackgroundWorkQueue();
        var controller = CreateController(context, CreatePrincipal("alice"), deferredQueue);

        var result = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "CFD"
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<EngineSimulationResponse>().Subject;
        deferredQueue.PendingWork.Should().ContainSingle();

        engine.IsActive = false;
        await context.SaveChangesAsync();

        await using var workerContext = CreateContext(databaseName);
        await deferredQueue.PendingWork[0].Work(
            new SingleServiceProvider(workerContext),
            CancellationToken.None);

        var simulation = await context.EngineSimulations.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == response.Id);
        simulation.Status.Should().Be("Failed");
        simulation.ErrorMessage.Should().Contain("inactive");
    }

    [Fact]
    public async Task RunSimulation_WhenBackgroundQueueIsFull_ReturnsServiceUnavailableWithoutCreatingSimulation()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Shared Engine",
            EngineType = "Test",
            CreatedBy = null
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var controller = CreateController(
            context,
            CreatePrincipal("alice"),
            new RejectingBackgroundWorkQueue());

        var result = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = engine.Id,
            SimulationType = "CFD"
        });

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.EngineSimulations.Should().BeEmpty();
    }

    [Fact]
    public async Task RunSimulation_WithTooManyParameters_ReturnsBadRequestWithoutCreatingSimulation()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.RunSimulation(new RunSimulationRequest
        {
            EngineId = 1,
            SimulationType = "CFD",
            Parameters = Enumerable.Range(0, RequestPayloadLimits.MaxDictionaryEntries + 1)
                .ToDictionary(index => $"parameter-{index}", index => (object)index)
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        JsonSerializer.Serialize(badRequest.Value).Should().Contain("Parameters");
        context.EngineSimulations.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSimulationStatus_WithStoredDiagnosticError_DoesNotExposeDetails()
    {
        await using var context = CreateContext();
        const string sensitiveError = "SQL connection failed for user admin with password secret";
        var simulation = await SeedSimulationAsync(context, "alice", "Running", sensitiveError);
        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.GetSimulationStatus(simulation.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var responseJson = JsonSerializer.Serialize(okResult.Value);
        responseJson.Should().NotContain(sensitiveError);
        using var response = JsonDocument.Parse(responseJson);
        response.RootElement.GetProperty("errorMessage").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetSimulationById_WithTelemetry_DoesNotExposeSimulationNavigationOrDiagnosticError()
    {
        await using var context = CreateContext();
        const string sensitiveError = "SQL connection failed for user admin with password secret";
        var simulation = await SeedSimulationAsync(context, "alice", "Failed", sensitiveError);
        context.EngineTelemetry.Add(new HB_NLP_Research_Lab.WebAPI.Data.Models.EngineTelemetry
        {
            SimulationId = simulation.Id,
            Timestamp = DateTime.UtcNow,
            Thrust = 100,
            Temperature = 300,
            Simulation = simulation
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.GetSimulationById(simulation.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EngineSimulationResponse>().Subject;
        response.ErrorMessage.Should().Be("Simulation failed. See server logs for details.");
        response.Telemetry.Should().ContainSingle();

        var responseJson = JsonSerializer.Serialize(response);
        responseJson.Should().NotContain(sensitiveError);
        using var document = JsonDocument.Parse(responseJson);
        var telemetryItem = document.RootElement.GetProperty("Telemetry")[0];
        telemetryItem.TryGetProperty("Simulation", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetSimulationById_WithManyTelemetrySamples_ReturnsBoundedTelemetry()
    {
        await using var context = CreateContext();
        var simulation = await SeedSimulationAsync(context, "alice", "Completed");
        var start = DateTime.UtcNow.AddMinutes(-120);
        for (var index = 0; index < 105; index++)
        {
            context.EngineTelemetry.Add(new HB_NLP_Research_Lab.WebAPI.Data.Models.EngineTelemetry
            {
                SimulationId = simulation.Id,
                Timestamp = start.AddMinutes(index),
                Thrust = index,
                Simulation = simulation
            });
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var controller = CreateController(context, CreatePrincipal("alice"));

        var result = await controller.GetSimulationById(simulation.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<EngineSimulationResponse>().Subject;
        response.Telemetry.Should().NotBeNull();
        var telemetry = response.Telemetry!.ToList();
        telemetry.Should().HaveCount(EngineSimulationResponse.MaxTelemetrySamples);
        telemetry.Should().OnlyContain(sample => sample.Thrust >= 5);

        // Query-side Take means the response cannot exceed the bound even if the
        // DTO mapper were to stop trimming — assert against the persisted overflow.
        (await context.EngineTelemetry.CountAsync(t => t.SimulationId == simulation.Id))
            .Should().Be(105);
    }

    [Fact]
    public void EngineSimulationSerialization_ExcludesStackTrace()
    {
        var simulation = new EngineSimulation
        {
            EngineId = 1,
            SimulationType = "CFD",
            Status = "Failed",
            ErrorMessage = "Simulation failed. See server logs for details.",
            StackTrace = "secret stack trace"
        };

        var json = JsonSerializer.Serialize(simulation);

        json.Should().NotContain("StackTrace");
        json.Should().NotContain("secret stack trace");
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

    private static async Task<EngineSimulation> SeedSimulationAsync(
        HelloblueGKDbContext context,
        string createdBy,
        string status,
        string? errorMessage = null)
    {
        var engine = new Engine
        {
            Name = $"{createdBy}-engine",
            EngineType = "Test",
            CreatedBy = createdBy
        };

        var simulation = new EngineSimulation
        {
            Engine = engine,
            SimulationType = "CFD",
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };

        context.EngineSimulations.Add(simulation);
        await context.SaveChangesAsync();
        return simulation;
    }

    private static SimulationsController CreateController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user,
        IBackgroundWorkQueue? backgroundWorkQueue = null)
    {
        return new SimulationsController(
            context,
            new HelloblueGKEngine(),
            NullLogger<SimulationsController>.Instance,
            backgroundWorkQueue ?? new RejectingBackgroundWorkQueue())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string username, bool isAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new("username", username)
        };

        claims.Add(new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ClaimsPrincipal CreatePrincipalWithoutUsername()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "User")
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
    }

    private sealed class DeferredBackgroundWorkQueue : IBackgroundWorkQueue
    {
        public int MaxConcurrency => 1;
        public List<(Func<IServiceProvider, CancellationToken, Task> Work, string Name)> PendingWork { get; } = new();

        public bool TryAcquire(out IBackgroundWorkSlot? slot)
        {
            slot = new DeferredBackgroundWorkSlot(this);
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
}
