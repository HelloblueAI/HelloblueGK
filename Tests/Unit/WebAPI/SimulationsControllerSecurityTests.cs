using System.Security.Claims;
using System.Text.Json;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Services;
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
    public async Task CancelSimulation_ForDifferentStandardUser_ReturnsForbid()
    {
        await using var context = CreateContext();
        var simulation = await SeedSimulationAsync(context, "alice", "Running");

        var controller = CreateController(context, CreatePrincipal("bob"));

        var result = await controller.CancelSimulation(simulation.Id);

        result.Should().BeOfType<ForbidResult>();
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
    public async Task RunSimulation_ForEngineOwnedByDifferentUser_ReturnsForbidWithoutCreatingSimulation()
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

        result.Should().BeOfType<ForbidResult>();
        context.EngineSimulations.Should().BeEmpty();
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

    private static HelloblueGKDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HelloblueGKDbContext(options);
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

        public bool TryAcquire(out BackgroundWorkSlot? slot)
        {
            slot = null;
            return false;
        }
    }
}
