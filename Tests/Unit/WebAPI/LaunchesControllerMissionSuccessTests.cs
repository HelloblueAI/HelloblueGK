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
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class LaunchesControllerMissionSuccessTests
{
    [Fact]
    public void HasTrustedValidationEvidence_RejectsSimulatedCollectorSources()
    {
        LaunchesController.HasTrustedValidationEvidence(null).Should().BeFalse();
        LaunchesController.HasTrustedValidationEvidence(new ValidationReport
        {
            ValidationSource = "Multiple Sources",
            OverallAccuracy = 99.9
        }).Should().BeFalse();
        LaunchesController.HasTrustedValidationEvidence(new ValidationReport
        {
            ValidationSource = "Real-Time Flight Telemetry",
            OverallAccuracy = 98.0
        }).Should().BeFalse();
        LaunchesController.HasTrustedValidationEvidence(new ValidationReport
        {
            ValidationSource = "Internal Simulation Database",
            OverallAccuracy = 97.0
        }).Should().BeFalse();
    }

    [Fact]
    public void HasTrustedValidationEvidence_AcceptsExplicitTrustedPrefix()
    {
        LaunchesController.HasTrustedValidationEvidence(new ValidationReport
        {
            ValidationSource = $"{LaunchesController.TrustedValidationSourcePrefix}FlightTelemetry",
            OverallAccuracy = 96.0
        }).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteLaunch_HighEfficiency_FailsClosedWithoutTrustedValidation()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using var context = CreateContext(databaseName);
        var launch = await SeedLaunchAsync(context, "admin");
        launch.Engine.Efficiency = 0.97;
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
        document.RootElement.GetProperty("engineEfficiency").GetDouble().Should().BeApproximately(0.97, 0.0001);
        document.RootElement.GetProperty("validationTrusted").GetBoolean().Should().BeFalse();
        document.RootElement.GetProperty("validationSource").GetString().Should().NotStartWith(
            LaunchesController.TrustedValidationSourcePrefix);
    }

    private static HelloblueGKDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite($"Data Source=file:{(databaseName ?? Guid.NewGuid().ToString("N"))}?mode=memory&cache=shared")
            .Options;

        var context = new HelloblueGKDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task<Launch> SeedLaunchAsync(HelloblueGKDbContext context, string createdBy)
    {
        var engine = new Engine
        {
            Name = $"{createdBy}-engine-{Guid.NewGuid():N}",
            EngineType = "Test",
            CreatedBy = createdBy,
            Thrust = 1,
            SpecificImpulse = 1,
            ChamberPressure = 1,
            Efficiency = 0.95,
            ExpansionRatio = 1,
            MassFlowRate = 1
        };
        var launch = new Launch
        {
            Engine = engine,
            MissionName = $"{createdBy} mission",
            Status = "Scheduled",
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };

        context.Launches.Add(launch);
        await context.SaveChangesAsync();
        return launch;
    }

    private static LaunchesController CreateLaunchesController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user,
        IBackgroundWorkQueue backgroundWorkQueue)
    {
        return new LaunchesController(
            context,
            new HelloblueGKEngine(),
            NullLogger<LaunchesController>.Instance,
            backgroundWorkQueue)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string username, bool isAdmin)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("username", username),
            new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
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

        public bool TryCancel(string workItemName) => true;
    }

    private sealed class DeferredBackgroundWorkSlot : IBackgroundWorkSlot
    {
        private readonly DeferredBackgroundWorkQueue _owner;
        private int _state;

        public DeferredBackgroundWorkSlot(DeferredBackgroundWorkQueue owner)
        {
            _owner = owner;
        }

        public void Queue(Func<IServiceProvider, CancellationToken, Task> workItem, string workItemName)
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
            return serviceType == typeof(HelloblueGKDbContext) ? _context : null;
        }
    }
}
