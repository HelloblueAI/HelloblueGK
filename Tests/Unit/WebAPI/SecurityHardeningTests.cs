using System.Net;
using System.Security.Claims;
using System.Text.Json;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Extensions;
using HB_NLP_Research_Lab.WebAPI.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class SecurityHardeningTests
{
    [Fact]
    public async Task GlobalExceptionHandler_InProduction_DoesNotExposeArgumentExceptionDetails()
    {
        const string sensitiveMessage = "database shard secret detail";
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new ArgumentException(sensitiveMessage),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production });

        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        body.Position = 0;
        using var response = await JsonDocument.ParseAsync(body);
        var responseText = response.RootElement.ToString();

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        response.RootElement.GetProperty("message").GetString()
            .Should().Be("The request could not be processed");
        responseText.Should().NotContain(sensitiveMessage);
    }

    [Fact]
    public void AddHelloblueGKAuthentication_WhenOidcEnabledWithoutAudience_Throws()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:OpenIdConnect:Enabled"] = "true",
            ["Authentication:OpenIdConnect:Authority"] = "https://identity.example.com",
            ["Authentication:OpenIdConnect:ClientId"] = "hellobluegk"
        });

        var act = () => builder.AddHelloblueGKAuthentication(
            "01234567890123456789012345678901",
            "hellobluegk",
            "hellobluegk-api");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenIdConnect:Audience*");
    }

    [Fact]
    public void AddHelloblueGKAuthentication_WhenOidcEnabledOutsideDevelopmentWithoutCallbackUrl_Throws()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:OpenIdConnect:Enabled"] = "true",
            ["Authentication:OpenIdConnect:Authority"] = "https://identity.example.com",
            ["Authentication:OpenIdConnect:ClientId"] = "hellobluegk",
            ["Authentication:OpenIdConnect:Audience"] = "api://hellobluegk"
        });

        var act = () => builder.AddHelloblueGKAuthentication(
            "01234567890123456789012345678901",
            "hellobluegk",
            "hellobluegk-api");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenIdConnect:CallbackUrl*");
    }

    [Fact]
    public async Task Swagger_InProduction_RequiresAuthentication()
    {
        using var factory = new TestWebApiFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var jsonResponse = await client.GetAsync("/swagger/v1/swagger.json");
        var uiResponse = await client.GetAsync("/swagger/index.html");

        jsonResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        uiResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Swagger_InDevelopment_AllowsPublicAccessWhenConfigured()
    {
        using var factory = new TestWebApiFactory(Environments.Development, new Dictionary<string, string?>
        {
            ["Documentation:AllowPublicInDevelopment"] = "true"
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"openapi\"");
    }

    [Fact]
    public async Task CreateDigitalTwin_ForDifferentUsersOnSameEngine_UsesSeparateEngineKeys()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("shared");
        context.Engines.Add(engine);
        await context.SaveChangesAsync();
        var digitalTwinEngine = new DigitalTwinEngine();

        var aliceController = CreateDigitalTwinController(context, CreatePrincipal("alice"), digitalTwinEngine);
        var bobController = CreateDigitalTwinController(context, CreatePrincipal("bob"), digitalTwinEngine);

        var aliceResult = await aliceController.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Alice Twin"
        });
        var bobResult = await bobController.CreateDigitalTwin(new CreateDigitalTwinRequest
        {
            EngineId = engine.Id,
            Name = "Bob Twin"
        });

        var aliceTwin = aliceResult.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwin>().Subject;
        var bobTwin = bobResult.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwin>().Subject;

        var aliceEngineKey = ReadStoredEngineKey(aliceTwin);
        var bobEngineKey = ReadStoredEngineKey(bobTwin);

        aliceEngineKey.Should().NotBe(bobEngineKey);
        aliceEngineKey.Should().NotBe($"Engine_{engine.Id}");
        bobEngineKey.Should().NotBe($"Engine_{engine.Id}");
    }

    private static string? ReadStoredEngineKey(DigitalTwin digitalTwin)
    {
        using var modelData = JsonDocument.Parse(digitalTwin.ModelDataJson!);
        return modelData.RootElement.GetProperty("EngineId").GetString();
    }

    private static HelloblueGKDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HelloblueGKDbContext(options);
    }

    private static DigitalTwinController CreateDigitalTwinController(
        HelloblueGKDbContext context,
        ClaimsPrincipal user,
        DigitalTwinEngine digitalTwinEngine)
    {
        return new DigitalTwinController(
            context,
            digitalTwinEngine,
            NullLogger<DigitalTwinController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static ClaimsPrincipal CreatePrincipal(string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new("username", username),
            new(ClaimTypes.Role, "User")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
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

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HelloblueGK.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestWebApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _environmentName;
        private readonly Dictionary<string, string?> _overrides;
        private readonly string _databasePath;

        public TestWebApiFactory(string environmentName, Dictionary<string, string?>? overrides = null)
        {
            _environmentName = environmentName;
            _overrides = overrides ?? new Dictionary<string, string?>();
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                $"hellobluegk-webapi-{Guid.NewGuid():N}.db");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environmentName);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                    ["Jwt:Key"] = "01234567890123456789012345678901",
                    ["Jwt:Issuer"] = "hellobluegk",
                    ["Jwt:Audience"] = "hellobluegk-api",
                    ["EnableRateLimiting"] = "false"
                };

                foreach (var (key, value) in _overrides)
                {
                    values[key] = value;
                }

                configurationBuilder.AddInMemoryCollection(values);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for SQLite files after the test server has disposed.
            }
        }
    }
}
