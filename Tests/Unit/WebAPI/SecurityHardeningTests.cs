using System.Reflection;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Configuration;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Extensions;
using HB_NLP_Research_Lab.WebAPI.Middleware;
using HB_NLP_Research_Lab.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class SecurityHardeningTests
{
    [Fact]
    public async Task Login_WithLegacySha256HashOutsideDevelopment_ReturnsUnauthorizedAndDoesNotUpgrade()
    {
        await using var context = CreateContext();
        var legacyHash = CreateLegacySha256Hash("correct-password");
        var user = new User
        {
            Username = "legacy",
            Email = "legacy@example.com",
            PasswordHash = legacyHash,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        var controller = CreateAuthController(context, jwtService.Object, Environments.Production);

        var result = await controller.Login(new LoginRequest
        {
            Username = "legacy",
            Password = "correct-password"
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        user.PasswordHash.Should().Be(legacyHash);
        user.LastLoginAt.Should().BeNull();
        user.UpdatedAt.Should().BeNull();
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Never);
        jwtService.Verify(service => service.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Login_WithLegacySha256HashInDevelopment_UpgradesToPbkdf2()
    {
        await using var context = CreateContext();
        var legacyHash = CreateLegacySha256Hash("correct-password");
        var user = new User
        {
            Username = "legacy",
            Email = "legacy@example.com",
            PasswordHash = legacyHash,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.GenerateToken(It.Is<User>(candidate => candidate.Username == "legacy")))
            .Returns("jwt-token");
        jwtService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("refresh-token");
        var controller = CreateAuthController(context, jwtService.Object, Environments.Development);

        var result = await controller.Login(new LoginRequest
        {
            Username = "legacy",
            Password = "correct-password"
        });

        result.Should().BeOfType<OkObjectResult>();
        user.PasswordHash.Should().NotBe(legacyHash);
        user.PasswordHash.Split(':').Should().HaveCount(3);
        user.LastLoginAt.Should().NotBeNull();
        user.UpdatedAt.Should().NotBeNull();
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Once);
        jwtService.Verify(service => service.GenerateRefreshToken(), Times.Once);
    }

    [Theory]
    [InlineData(nameof(EnginesController.GetAllEngines))]
    [InlineData(nameof(EnginesController.GetActiveEngines))]
    [InlineData(nameof(EnginesController.GetEngineById))]
    [InlineData(nameof(EnginesController.GetEngineByName))]
    public void EngineReadActions_RequireExplicitAuthorization(string actionName)
    {
        AssertActionRequiresAuthorize<EnginesController>(actionName);
    }

    [Theory]
    [InlineData(nameof(HealthController.GetDetailed))]
    [InlineData(nameof(HealthController.GetEngineHealth))]
    public void SensitiveHealthActions_RequireAdminRole(string actionName)
    {
        AssertActionRequiresRole<HealthController>(actionName, "Admin");
    }

    [Fact]
    public void BasicHealthAction_AllowsAnonymousAccess()
    {
        typeof(HealthController).GetMethod(nameof(HealthController.Get))!
            .GetCustomAttributes<AllowAnonymousAttribute>()
            .Should().NotBeEmpty();
    }

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
    public async Task ForwardedHeaders_ByDefault_IgnoresSpoofedClientIp()
    {
        var observedClientIp = await RunForwardedHeaderPipelineAsync(
            new Dictionary<string, string?>(),
            Environments.Production,
            proxyIp: "203.0.113.10",
            forwardedFor: "198.51.100.25");

        observedClientIp.Should().Be("203.0.113.10");
    }

    [Fact]
    public async Task ForwardedHeaders_WithKnownProxy_TrustsForwardedClientIp()
    {
        var observedClientIp = await RunForwardedHeaderPipelineAsync(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies"] = "203.0.113.10"
            },
            Environments.Production,
            proxyIp: "203.0.113.10",
            forwardedFor: "198.51.100.25");

        observedClientIp.Should().Be("198.51.100.25");
    }

    [Fact]
    public void ForwardedHeaders_WithTrustAllOutsideDevelopment_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:TrustAll"] = "true"
            })
            .Build();
        var options = new ForwardedHeadersOptions();

        var act = () => ForwardedHeadersConfiguration.Configure(
            options,
            configuration,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TrustAll*Development*");
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

    private static AuthController CreateAuthController(
        HelloblueGKDbContext context,
        IJwtService jwtService,
        string environmentName)
    {
        return new AuthController(
            context,
            jwtService,
            NullLogger<AuthController>.Instance,
            new TestWebHostEnvironment { EnvironmentName = environmentName },
            new ConfigurationBuilder().Build())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Method = HttpMethods.Post,
                        Path = "/api/v1/auth/login"
                    }
                }
            }
        };
    }

    private static string CreateLegacySha256Hash(string password)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }

    private static void AssertActionRequiresAuthorize<TController>(string actionName)
    {
        typeof(TController).GetMethod(actionName)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Should().NotBeEmpty();
    }

    private static void AssertActionRequiresRole<TController>(string actionName, string role)
    {
        var authorizeAttributes = typeof(TController).GetMethod(actionName)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        authorizeAttributes.Should().NotBeEmpty();
        authorizeAttributes
            .Should().Contain(attribute =>
                attribute.Roles != null && attribute.Roles.Contains(role, StringComparison.Ordinal));
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

    private static async Task<string> RunForwardedHeaderPipelineAsync(
        Dictionary<string, string?> configurationValues,
        string environmentName,
        string proxyIp,
        string forwardedFor)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var environment = new TestWebHostEnvironment { EnvironmentName = environmentName };
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.Configure<ForwardedHeadersOptions>(options =>
            ForwardedHeadersConfiguration.Configure(options, configuration, environment));

        using var provider = services.BuildServiceProvider();
        var applicationBuilder = new ApplicationBuilder(provider);
        applicationBuilder.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(proxyIp);
            await next(context);
        });
        applicationBuilder.UseForwardedHeaders();
        applicationBuilder.Run(context =>
            context.Response.WriteAsync(context.Connection.RemoteIpAddress?.ToString() ?? "unknown"));

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await applicationBuilder.Build()(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return await reader.ReadToEndAsync();
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
        private readonly Dictionary<string, string?> _previousEnvironmentValues = new();

        public TestWebApiFactory(string environmentName, Dictionary<string, string?>? overrides = null)
        {
            _environmentName = environmentName;
            _overrides = overrides ?? new Dictionary<string, string?>();
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                $"hellobluegk-webapi-{Guid.NewGuid():N}.db");

            foreach (var (key, value) in CreateConfigurationValues())
            {
                var environmentKey = key.Replace(":", "__", StringComparison.Ordinal);
                _previousEnvironmentValues[environmentKey] = Environment.GetEnvironmentVariable(environmentKey);
                Environment.SetEnvironmentVariable(environmentKey, value);
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environmentName);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(CreateConfigurationValues());
            });
        }

        private Dictionary<string, string?> CreateConfigurationValues()
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

            return values;
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

                foreach (var (key, value) in _previousEnvironmentValues)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup for SQLite files after the test server has disposed.
            }
        }
    }
}
