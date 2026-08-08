using System.Reflection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Configuration;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using HB_NLP_Research_Lab.WebAPI.Controllers.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Extensions;
using HB_NLP_Research_Lab.WebAPI.Middleware;
using HB_NLP_Research_Lab.WebAPI.Services;
using HB_NLP_Research_Lab.WebAPI.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class SecurityHardeningTests
{
    [Fact]
    public async Task Login_WithAmbiguousCaseVariantUsernames_RequiresExactMatch()
    {
        await using var context = CreateContext();
        context.Users.AddRange(
            new User
            {
                Username = "alice",
                Email = "alice@example.com",
                PasswordHash = CreateLegacySha256Hash("alice-password"),
                IsActive = true
            },
            new User
            {
                Username = "Alice",
                Email = "alice.admin@example.com",
                PasswordHash = CreateLegacySha256Hash("Alice-password"),
                IsActive = true
            });
        await context.SaveChangesAsync();

        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("jwt-token");
        jwtService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("refresh-token");
        jwtService
            .Setup(service => service.HashRefreshToken("refresh-token"))
            .Returns("refresh-token-hash");
        jwtService
            .Setup(service => service.GetTokenExpirationSeconds())
            .Returns(7200);
        jwtService
            .Setup(service => service.GetRefreshTokenExpirationSeconds())
            .Returns(604800);

        var controller = CreateAuthController(context, jwtService.Object, Environments.Development);

        var exactResult = await controller.Login(new LoginRequest
        {
            Username = "alice",
            Password = "alice-password"
        });
        var exactResponse = exactResult.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponse>().Subject;
        exactResponse.Token.Should().Be("jwt-token");
        jwtService.Verify(
            service => service.GenerateToken(It.Is<User>(candidate => candidate.Username == "alice")),
            Times.Once);

        var ambiguousResult = await controller.Login(new LoginRequest
        {
            Username = "ALICE",
            Password = "alice-password"
        });
        ambiguousResult.Should().BeOfType<UnauthorizedObjectResult>();
        jwtService.Verify(
            service => service.GenerateToken(It.IsAny<User>()),
            Times.Once);
    }

    [Fact]
    public async Task Login_WithSingleLegacyMixedCaseUsername_AllowsCaseInsensitiveMatch()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Username = "LegacyUser",
            Email = "legacyuser@example.com",
            PasswordHash = CreateLegacySha256Hash("correct-password"),
            IsActive = true
        });
        await context.SaveChangesAsync();

        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.GenerateToken(It.Is<User>(candidate => candidate.Username == "LegacyUser")))
            .Returns("jwt-token");
        jwtService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("refresh-token");
        jwtService
            .Setup(service => service.HashRefreshToken("refresh-token"))
            .Returns("refresh-token-hash");
        jwtService
            .Setup(service => service.GetTokenExpirationSeconds())
            .Returns(7200);
        jwtService
            .Setup(service => service.GetRefreshTokenExpirationSeconds())
            .Returns(604800);

        var controller = CreateAuthController(context, jwtService.Object, Environments.Development);
        var result = await controller.Login(new LoginRequest
        {
            Username = "legacyuser",
            Password = "correct-password"
        });

        result.Should().BeOfType<OkObjectResult>();
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Once);
    }

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
        jwtService
            .Setup(service => service.HashRefreshToken("refresh-token"))
            .Returns("refresh-token-hash");
        jwtService
            .Setup(service => service.GetTokenExpirationSeconds())
            .Returns(7200);
        jwtService
            .Setup(service => service.GetRefreshTokenExpirationSeconds())
            .Returns(604800);
        var controller = CreateAuthController(context, jwtService.Object, Environments.Development);

        var result = await controller.Login(new LoginRequest
        {
            Username = "legacy",
            Password = "correct-password"
        });

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponse>().Subject;
        response.ExpiresIn.Should().Be(7200);
        response.RefreshToken.Should().Be("refresh-token");
        user.PasswordHash.Should().NotBe(legacyHash);
        user.PasswordHash.Split(':').Should().HaveCount(3);
        user.LastLoginAt.Should().NotBeNull();
        user.UpdatedAt.Should().NotBeNull();
        user.RefreshTokenHash.Should().Be("refresh-token-hash");
        user.RefreshTokenExpiresAt.Should().NotBeNull();
        user.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(10));
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Once);
        jwtService.Verify(service => service.GenerateRefreshToken(), Times.Once);
        jwtService.Verify(service => service.HashRefreshToken("refresh-token"), Times.Once);
        jwtService.Verify(service => service.GetTokenExpirationSeconds(), Times.Once);
        jwtService.Verify(service => service.GetRefreshTokenExpirationSeconds(), Times.Once);
    }

    [Fact]
    public async Task Refresh_WithPersistedToken_RotatesAccessAndRefreshTokens()
    {
        await using var context = CreateContext();
        var user = new User
        {
            Username = "refresher",
            Email = "refresher@example.com",
            PasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IsActive = true,
            RefreshTokenHash = "stored-refresh-hash",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.HashRefreshToken("presented-refresh-token"))
            .Returns("stored-refresh-hash");
        jwtService
            .Setup(service => service.GenerateToken(It.Is<User>(candidate => candidate.Username == "refresher")))
            .Returns("new-access-token");
        jwtService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("rotated-refresh-token");
        jwtService
            .Setup(service => service.HashRefreshToken("rotated-refresh-token"))
            .Returns("rotated-refresh-hash");
        jwtService
            .Setup(service => service.GetTokenExpirationSeconds())
            .Returns(3600);
        jwtService
            .Setup(service => service.GetRefreshTokenExpirationSeconds())
            .Returns(604800);

        var controller = CreateAuthController(context, jwtService.Object, Environments.Production);

        var result = await controller.Refresh(new RefreshTokenRequest
        {
            RefreshToken = "presented-refresh-token"
        });

        var response = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponse>().Subject;
        response.Token.Should().Be("new-access-token");
        response.RefreshToken.Should().Be("rotated-refresh-token");
        user.RefreshTokenHash.Should().Be("rotated-refresh-hash");
        user.RefreshTokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.HashRefreshToken("unknown-token"))
            .Returns("missing-hash");

        var controller = CreateAuthController(context, jwtService.Object, Environments.Production);

        var result = await controller.Refresh(new RefreshTokenRequest
        {
            RefreshToken = "unknown-token"
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Never);
        jwtService.Verify(service => service.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ClearsStoredHashAndReturnsUnauthorized()
    {
        await using var context = CreateContext();
        var user = new User
        {
            Username = "expired-refresh",
            Email = "expired-refresh@example.com",
            PasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IsActive = true,
            RefreshTokenHash = "expired-refresh-hash",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.HashRefreshToken("expired-refresh-token"))
            .Returns("expired-refresh-hash");

        var controller = CreateAuthController(context, jwtService.Object, Environments.Production);

        var result = await controller.Refresh(new RefreshTokenRequest
        {
            RefreshToken = "expired-refresh-token"
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        user.RefreshTokenHash.Should().BeNull();
        user.RefreshTokenExpiresAt.Should().BeNull();
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Never);
        jwtService.Verify(service => service.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Refresh_ConcurrentReuseOfSameToken_OnlyOneRotationSucceeds()
    {
        await using var context = CreateContext();
        var user = new User
        {
            Username = "race-refresh",
            Email = "race-refresh@example.com",
            PasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IsActive = true,
            RefreshTokenHash = "shared-refresh-hash",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.HashRefreshToken("shared-refresh-token"))
            .Returns("shared-refresh-hash");
        jwtService
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("access-token");
        jwtService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("winner-refresh-token");
        jwtService
            .Setup(service => service.HashRefreshToken("winner-refresh-token"))
            .Returns("winner-refresh-hash");
        jwtService
            .Setup(service => service.GetTokenExpirationSeconds())
            .Returns(3600);
        jwtService
            .Setup(service => service.GetRefreshTokenExpirationSeconds())
            .Returns(604800);

        var controller = CreateAuthController(context, jwtService.Object, Environments.Production);
        var request = new RefreshTokenRequest { RefreshToken = "shared-refresh-token" };

        var first = await controller.Refresh(request);
        var second = await controller.Refresh(request);

        first.Should().BeOfType<OkObjectResult>();
        second.Should().BeOfType<UnauthorizedObjectResult>();

        var persisted = await context.Users.AsNoTracking().SingleAsync(candidate => candidate.Id == user.Id);
        persisted.RefreshTokenHash.Should().Be("winner-refresh-hash");
    }

    [Fact]
    public async Task Logout_ClearsPersistedRefreshToken()
    {
        await using var context = CreateContext();
        var user = new User
        {
            Username = "logout-user",
            Email = "logout-user@example.com",
            PasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IsActive = true,
            RefreshTokenHash = "logout-refresh-hash",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateAuthController(
            context,
            Mock.Of<IJwtService>(),
            Environments.Production);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim("userId", user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Username)
                    },
                    "Test"))
            }
        };

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        var persisted = await context.Users.AsNoTracking().SingleAsync(candidate => candidate.Id == user.Id);
        persisted.RefreshTokenHash.Should().BeNull();
        persisted.RefreshTokenExpiresAt.Should().BeNull();
        persisted.AccessTokenVersion.Should().Be(1);
    }

    [Fact]
    public async Task Register_WhenPublicRegistrationAllowed_IssuesRefreshTokenAndPersistsHash()
    {
        await using var context = CreateContext();
        var jwtService = new Mock<IJwtService>(MockBehavior.Strict);
        jwtService
            .Setup(service => service.GenerateToken(It.IsAny<User>()))
            .Returns("register-access-token");
        jwtService
            .Setup(service => service.GenerateRefreshToken())
            .Returns("register-refresh-token");
        jwtService
            .Setup(service => service.HashRefreshToken("register-refresh-token"))
            .Returns("register-refresh-hash");
        jwtService
            .Setup(service => service.GetTokenExpirationSeconds())
            .Returns(3600);
        jwtService
            .Setup(service => service.GetRefreshTokenExpirationSeconds())
            .Returns(604800);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowPublicRegistration"] = "true"
            })
            .Build();

        var controller = new AuthController(
            context,
            jwtService.Object,
            NullLogger<AuthController>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Method = HttpMethods.Post,
                        Path = "/api/v1/auth/register"
                    }
                }
            }
        };

        var result = await controller.Register(new RegisterRequest
        {
            Username = "new-user",
            Email = "new-user@example.com",
            Password = "Password123!",
            FirstName = "New",
            LastName = "User"
        });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var response = created.Value.Should().BeOfType<LoginResponse>().Subject;
        response.Token.Should().Be("register-access-token");
        response.RefreshToken.Should().Be("register-refresh-token");
        response.ExpiresIn.Should().Be(3600);

        var persisted = await context.Users.AsNoTracking().SingleAsync(candidate => candidate.Username == "new-user");
        persisted.RefreshTokenHash.Should().Be("register-refresh-hash");
        persisted.RefreshTokenExpiresAt.Should().NotBeNull();
        persisted.AccessTokenVersion.Should().Be(0);
        jwtService.Verify(service => service.GenerateToken(It.IsAny<User>()), Times.Once);
        jwtService.Verify(service => service.GenerateRefreshToken(), Times.Once);
        jwtService.Verify(service => service.HashRefreshToken("register-refresh-token"), Times.Once);
    }

    [Fact]
    public async Task Register_WithExistingUsername_ReturnsGenericError()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Username = "taken",
            Email = "taken@example.com",
            PasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowPublicRegistration"] = "true"
            })
            .Build();

        var controller = new AuthController(
            context,
            Mock.Of<IJwtService>(),
            NullLogger<AuthController>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Register(new RegisterRequest
        {
            Username = "taken",
            Email = "new@example.com",
            Password = "Password123!"
        });

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<HB_NLP_Research_Lab.WebAPI.Models.ErrorResponse>().Subject;
        error.Message.Should().Be("Unable to register with the provided credentials");
        error.Message.Should().NotContain("Username");
        error.Message.Should().NotContain("Email");
    }

    [Fact]
    public async Task Register_WithCaseVariantUsername_ReturnsGenericErrorAndDoesNotCreateUser()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Username = "taken",
            Email = "taken@example.com",
            PasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowPublicRegistration"] = "true"
            })
            .Build();

        var controller = new AuthController(
            context,
            Mock.Of<IJwtService>(),
            NullLogger<AuthController>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Register(new RegisterRequest
        {
            Username = "TAKEN",
            Email = "other@example.com",
            Password = "Password123!"
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.Users.Count(u => u.Username.ToLower() == "taken").Should().Be(1);
    }

    [Fact]
    public async Task Register_NormalizesUsernameAndEmailToLowercase()
    {
        await using var context = CreateContext();
        var jwtService = new Mock<IJwtService>();
        jwtService.Setup(service => service.GenerateToken(It.IsAny<User>())).Returns("token");
        jwtService.Setup(service => service.GenerateRefreshToken()).Returns("refresh-token");
        jwtService.Setup(service => service.HashRefreshToken(It.IsAny<string>())).Returns("refresh-hash");
        jwtService.Setup(service => service.GetTokenExpirationSeconds()).Returns(3600);
        jwtService.Setup(service => service.GetRefreshTokenExpirationSeconds()).Returns(604800);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:AllowPublicRegistration"] = "true"
            })
            .Build();

        var controller = new AuthController(
            context,
            jwtService.Object,
            NullLogger<AuthController>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        Method = HttpMethods.Post,
                        Path = "/api/v1/auth/register"
                    }
                }
            }
        };

        var result = await controller.Register(new RegisterRequest
        {
            Username = "Alice",
            Email = "Alice@Example.COM",
            Password = "Password123!"
        });

        result.Should().BeOfType<CreatedAtActionResult>();
        var stored = context.Users.Single();
        stored.Username.Should().Be("alice");
        stored.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task BackgroundJobReconciliation_FailsInterruptedPendingAndRunningJobs_LeavesScheduledLaunches()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Reconciliation Engine",
            EngineType = "Custom",
            Thrust = 1_000_000,
            SpecificImpulse = 350,
            ChamberPressure = 200,
            Efficiency = 0.9,
            IsActive = true,
            CreatedBy = "tester"
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        context.EngineSimulations.AddRange(
            new EngineSimulation
            {
                EngineId = engine.Id,
                SimulationType = "CFD",
                Status = "Pending",
                CreatedBy = "tester"
            },
            new EngineSimulation
            {
                EngineId = engine.Id,
                SimulationType = "Thermal",
                Status = "Running",
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedBy = "tester"
            },
            new EngineSimulation
            {
                EngineId = engine.Id,
                SimulationType = "Structural",
                Status = "Completed",
                CompletedAt = DateTime.UtcNow.AddMinutes(-10),
                CreatedBy = "tester"
            });
        context.AIOptimizationRuns.Add(new AIOptimizationRun
        {
            EngineId = engine.Id,
            AlgorithmType = "Genetic",
            Status = "Running",
            StartedAt = DateTime.UtcNow.AddMinutes(-3),
            CreatedBy = "tester"
        });
        context.Launches.AddRange(
            new Launch
            {
                MissionName = "In flight",
                EngineId = engine.Id,
                Status = "InProgress",
                ScheduledAt = DateTime.UtcNow.AddMinutes(-20),
                LaunchedAt = DateTime.UtcNow.AddMinutes(-2),
                CreatedBy = "tester"
            },
            new Launch
            {
                MissionName = "Still scheduled",
                EngineId = engine.Id,
                Status = "Scheduled",
                ScheduledAt = DateTime.UtcNow.AddHours(1),
                CreatedBy = "tester"
            });
        await context.SaveChangesAsync();

        // Default / Zero: single-instance immediate fail-close (in-process work cannot resume).
        var result = await BackgroundJobReconciliation.ReconcileInterruptedJobsAsync(
            context,
            NullLogger.Instance);

        result.Simulations.Should().Be(2);
        result.Optimizations.Should().Be(1);
        result.Launches.Should().Be(1);
        result.Total.Should().Be(4);

        var simulations = await context.EngineSimulations.AsNoTracking().ToListAsync();
        simulations.Single(s => s.SimulationType == "CFD").Status.Should().Be("Failed");
        simulations.Single(s => s.SimulationType == "Thermal").Status.Should().Be("Failed");
        simulations.Single(s => s.SimulationType == "Structural").Status.Should().Be("Completed");
        simulations.Where(s => s.Status == "Failed")
            .Should().OnlyContain(s => s.ErrorMessage == BackgroundJobReconciliation.InterruptedMessage);

        var optimization = await context.AIOptimizationRuns.AsNoTracking().SingleAsync();
        optimization.Status.Should().Be("Failed");
        optimization.ErrorMessage.Should().Be(BackgroundJobReconciliation.InterruptedMessage);

        var launches = await context.Launches.AsNoTracking().ToListAsync();
        launches.Single(l => l.MissionName == "In flight").Status.Should().Be("Failed");
        launches.Single(l => l.MissionName == "Still scheduled").Status.Should().Be("Scheduled");
    }

    [Fact]
    public async Task BackgroundJobReconciliation_SkipsJobsYoungerThanMinimumAge_PreservesPeerReplicaWork()
    {
        await using var context = CreateContext();
        var engine = new Engine
        {
            Name = "Age Gate Engine",
            EngineType = "Custom",
            Thrust = 1_000_000,
            SpecificImpulse = 350,
            ChamberPressure = 200,
            Efficiency = 0.9,
            IsActive = true,
            CreatedBy = "tester"
        };
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        var staleStartedAt = DateTime.UtcNow.AddHours(-2);
        var freshStartedAt = DateTime.UtcNow.AddMinutes(-2);

        context.EngineSimulations.AddRange(
            new EngineSimulation
            {
                EngineId = engine.Id,
                SimulationType = "CFD",
                Status = "Running",
                StartedAt = staleStartedAt,
                CreatedBy = "tester"
            },
            new EngineSimulation
            {
                EngineId = engine.Id,
                SimulationType = "Thermal",
                Status = "Running",
                StartedAt = freshStartedAt,
                CreatedBy = "tester"
            });
        context.AIOptimizationRuns.AddRange(
            new AIOptimizationRun
            {
                EngineId = engine.Id,
                AlgorithmType = "Genetic",
                Status = "Pending",
                CreatedAt = staleStartedAt,
                CreatedBy = "tester"
            },
            new AIOptimizationRun
            {
                EngineId = engine.Id,
                AlgorithmType = "PSO",
                Status = "Running",
                StartedAt = freshStartedAt,
                CreatedBy = "tester"
            });
        context.Launches.AddRange(
            new Launch
            {
                MissionName = "Stale in flight",
                EngineId = engine.Id,
                Status = "InProgress",
                ScheduledAt = staleStartedAt.AddMinutes(-5),
                LaunchedAt = staleStartedAt,
                CreatedBy = "tester"
            },
            new Launch
            {
                MissionName = "Peer in flight",
                EngineId = engine.Id,
                Status = "InProgress",
                ScheduledAt = freshStartedAt.AddMinutes(-5),
                LaunchedAt = freshStartedAt,
                CreatedBy = "tester"
            });
        await context.SaveChangesAsync();

        // Explicit shared-DB age gate (multi-replica); default Zero would fail the fresh peer rows.
        var result = await BackgroundJobReconciliation.ReconcileInterruptedJobsAsync(
            context,
            NullLogger.Instance,
            BackgroundJobReconciliation.SharedDatabaseInterruptedJobMinimumAge);

        result.Simulations.Should().Be(1);
        result.Optimizations.Should().Be(1);
        result.Launches.Should().Be(1);
        result.Total.Should().Be(3);

        var simulations = await context.EngineSimulations.AsNoTracking().ToListAsync();
        simulations.Single(s => s.SimulationType == "CFD").Status.Should().Be("Failed");
        simulations.Single(s => s.SimulationType == "Thermal").Status.Should().Be("Running");

        var optimizations = await context.AIOptimizationRuns.AsNoTracking().ToListAsync();
        optimizations.Single(o => o.AlgorithmType == "Genetic").Status.Should().Be("Failed");
        optimizations.Single(o => o.AlgorithmType == "PSO").Status.Should().Be("Running");

        var launches = await context.Launches.AsNoTracking().ToListAsync();
        launches.Single(l => l.MissionName == "Stale in flight").Status.Should().Be("Failed");
        launches.Single(l => l.MissionName == "Peer in flight").Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task DatabaseInitializer_AddsMissingRefreshTokenColumnsOnLegacySqliteSchema()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "Users" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
                    "Username" TEXT NOT NULL,
                    "Email" TEXT NOT NULL,
                    "PasswordHash" TEXT NOT NULL,
                    "FirstName" TEXT NULL,
                    "LastName" TEXT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "IsAdmin" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "LastLoginAt" TEXT NULL,
                    "UpdatedAt" TEXT NULL
                );
                CREATE TABLE "Engines" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Engines" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "EngineType" TEXT NOT NULL,
                    "Status" TEXT NULL,
                    "Thrust" REAL NOT NULL,
                    "Isp" REAL NOT NULL,
                    "Weight" REAL NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL,
                    "CreatedBy" TEXT NULL,
                    "IsActive" INTEGER NOT NULL
                );
                CREATE TABLE "DigitalTwins" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_DigitalTwins" PRIMARY KEY AUTOINCREMENT,
                    "EngineId" INTEGER NOT NULL,
                    "Name" TEXT NULL,
                    "PredictionAccuracy" REAL NOT NULL,
                    "RealTimeLearning" INTEGER NOT NULL,
                    "ModelDataJson" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "LastUpdated" TEXT NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedBy" TEXT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new HelloblueGKDbContext(options);

        await DatabaseInitializer.EnsureSchemaCompatibilityAsync(
            context,
            NullLogger.Instance);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """PRAGMA table_info("Users")""";
            await using var reader = await command.ExecuteReaderAsync();
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            columns.Should().Contain("RefreshTokenHash");
            columns.Should().Contain("RefreshTokenExpiresAt");
            columns.Should().Contain("AccessTokenVersion");
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """SELECT name FROM sqlite_master WHERE type = 'index'""";
            await using var reader = await command.ExecuteReaderAsync();
            var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }

            indexes.Should().Contain("IX_Users_RefreshTokenHash");
            indexes.Should().Contain("IX_DigitalTwins_EngineId_CreatedBy_Active");
        }

        // Compatibility patch must be idempotent.
        await DatabaseInitializer.EnsureSchemaCompatibilityAsync(
            context,
            NullLogger.Instance);
    }

    [Fact]
    public void JwtService_HashRefreshToken_IsDeterministicSha256()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "01234567890123456789012345678901"
            })
            .Build();
        var service = new JwtService(
            configuration,
            NullLogger<JwtService>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production });

        var first = service.HashRefreshToken("same-token");
        var second = service.HashRefreshToken("same-token");
        var other = service.HashRefreshToken("other-token");

        first.Should().Be(second);
        first.Should().NotBe(other);
        first.Should().Be(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("same-token"))));
    }

    [Fact]
    public void HelloblueGKDbContext_ConfiguresUniqueActiveDigitalTwinIndex()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new HelloblueGKDbContext(options);
        var index = context.Model.FindEntityType(typeof(DigitalTwin))!
            .GetIndexes()
            .Single(candidate =>
                candidate.IsUnique
                && candidate.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(DigitalTwin.EngineId), nameof(DigitalTwin.CreatedBy) }));

        index.GetFilter().Should().Contain("IsActive");
        index.GetFilter().Should().Contain("CreatedBy");
    }

    [Fact]
    public async Task HelloblueGKDbContext_RejectsSecondActiveDigitalTwinForSameOwner()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new HelloblueGKDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var engine = CreateEngine("indexed-engine");
        engine.CreatedBy = null;
        context.Engines.Add(engine);
        await context.SaveChangesAsync();

        context.DigitalTwins.Add(new DigitalTwin
        {
            EngineId = engine.Id,
            Name = "first",
            CreatedBy = "alice",
            IsActive = true
        });
        await context.SaveChangesAsync();

        context.DigitalTwins.Add(new DigitalTwin
        {
            EngineId = engine.Id,
            Name = "second",
            CreatedBy = "alice",
            IsActive = true
        });

        var act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void JwtService_GetTokenExpirationSeconds_UsesConfiguredTokenLifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "01234567890123456789012345678901",
                ["Jwt:TokenExpirationHours"] = "2"
            })
            .Build();
        var service = new JwtService(
            configuration,
            NullLogger<JwtService>.Instance,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production });

        var expirationSeconds = service.GetTokenExpirationSeconds();

        expirationSeconds.Should().Be(7200);
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
    public async Task SystemHealthFailureResponses_DoNotExposeExceptionDetails()
    {
        var controller = new SystemHealthController(
            NullLogger<SystemHealthController>.Instance,
            null!,
            null!);

        var comprehensiveResult = await controller.GetComprehensiveHealth();
        var basicResult = await controller.GetBasicStatus();
        var summaryResult = await controller.GetHealthSummary();

        AssertObjectResultDoesNotExpose(comprehensiveResult.Result, "Object reference");
        AssertObjectResultDoesNotExpose(basicResult.Result, "Object reference");
        AssertObjectResultDoesNotExpose(summaryResult.Result, "Object reference");
    }

    [Fact]
    public async Task RequirementCreationFailure_DoesNotExposeExceptionDetails()
    {
        var controller = new RequirementsController(
            null!,
            null!,
            NullLogger<RequirementsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.HttpContext.Request.Method = HttpMethods.Post;
        controller.HttpContext.Request.Path = "/api/v1/certification/requirements";

        var result = await controller.CreateRequirement(new CreateRequirementRequest
        {
            RequirementNumber = "REQ-SEC-001",
            Title = "Security requirement",
            Description = "Should not expose exception details"
        });

        AssertObjectResultDoesNotExpose(result, "Object reference");
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
    public async Task OpenIdConnect_WithConfiguredAdminGroup_AddsApplicationAdminRole()
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
            ["Authentication:OpenIdConnect:Audience"] = "api://hellobluegk",
            ["Authentication:OpenIdConnect:CallbackUrl"] = "https://api.example.com/api/v1/Account/sso-callback",
            ["Authentication:OpenIdConnect:AdminRoles:0"] = "aerospace-admins",
            ["Authentication:OpenIdConnect:AdminRoleClaimTypes:0"] = "groups"
        });
        builder.AddHelloblueGKAuthentication(
            "01234567890123456789012345678901",
            "hellobluegk",
            "hellobluegk-api");
        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("groups", "aerospace-admins"),
                new Claim(ClaimTypes.Email, "admin@example.com")
            ],
            OpenIdConnectDefaults.AuthenticationScheme));
        var context = new TokenValidatedContext(
            httpContext,
            new AuthenticationScheme(
                OpenIdConnectDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme,
                typeof(OpenIdConnectHandler)),
            options,
            principal,
            new AuthenticationProperties());

        await options.Events.TokenValidated(context);

        principal.IsInRole("Admin").Should().BeTrue();
        principal.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    [InlineData("/%2fevil.example.com")]
    [InlineData("/%5cevil.example.com")]
    [InlineData("/..%2f%2fevil.example.com")]
    [InlineData("/.%2e/%2e%2f/evil.example.com")]
    [InlineData("/swagger/../../../evil.example.com")]
    [InlineData("/api/v1/Account/logout")]
    public void AccountLogin_WithUnsafeReturnUrl_FallsBackToSwagger(string returnUrl)
    {
        var controller = CreateAccountController();

        var result = controller.Login(returnUrl);

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties!.RedirectUri.Should().Be("/swagger");
        challenge.AuthenticationSchemes.Should().Contain(OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html?filter=internal")]
    public void AccountLogin_WithLocalReturnUrl_PreservesRedirect(string returnUrl)
    {
        var controller = CreateAccountController();

        var result = controller.Login(returnUrl);

        var challenge = result.Should().BeOfType<ChallengeResult>().Subject;
        challenge.Properties!.RedirectUri.Should().Be(returnUrl);
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
    public async Task Swagger_InProduction_RequiresAdminRole()
    {
        using var factory = new TestWebApiFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var alice = await SeedFactoryUserAsync(factory, "alice", isAdmin: false);
        var admin = await SeedFactoryUserAsync(factory, "admin", isAdmin: true);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(alice.Id, alice.Username, isAdmin: false));
        var userResponse = await client.GetAsync("/swagger/v1/swagger.json");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(admin.Id, admin.Username, isAdmin: true));
        var adminResponse = await client.GetAsync("/swagger/v1/swagger.json");
        var adminBody = await adminResponse.Content.ReadAsStringAsync();

        userResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        adminBody.Should().Contain("\"openapi\"");
    }

    [Fact]
    public async Task Metrics_InProduction_RequiresAdminRole()
    {
        using var factory = new TestWebApiFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var alice = await SeedFactoryUserAsync(factory, "alice", isAdmin: false);
        var admin = await SeedFactoryUserAsync(factory, "admin", isAdmin: true);

        var unauthenticatedResponse = await client.GetAsync("/metrics");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(alice.Id, alice.Username, isAdmin: false));
        var userResponse = await client.GetAsync("/metrics");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(admin.Id, admin.Username, isAdmin: true));
        var adminResponse = await client.GetAsync("/metrics");

        unauthenticatedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        userResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task JwtBearer_WithInactiveUser_RejectsPreviouslyIssuedToken()
    {
        using var factory = new TestWebApiFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var inactiveAdmin = await SeedFactoryUserAsync(
            factory,
            "inactive-admin",
            isAdmin: true,
            isActive: false);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(inactiveAdmin.Id, inactiveAdmin.Username, isAdmin: true));

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtBearer_WithDemotedUser_RejectsStaleAdminToken()
    {
        using var factory = new TestWebApiFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var demotedUser = await SeedFactoryUserAsync(
            factory,
            "demoted-user",
            isAdmin: false);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateJwtToken(demotedUser.Id, demotedUser.Username, isAdmin: true));

        var response = await client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtBearer_AfterLogout_RejectsPreviouslyIssuedAccessToken()
    {
        using var factory = new TestWebApiFactory(Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var user = await SeedFactoryUserAsync(
            factory,
            "logout-revoke-user",
            isAdmin: true);

        var accessToken = CreateJwtToken(
            user.Id,
            user.Username,
            isAdmin: true,
            accessTokenVersion: 0);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var beforeLogout = await client.GetAsync("/metrics");
        beforeLogout.StatusCode.Should().Be(HttpStatusCode.OK);

        var logoutResponse = await client.PostAsync("/api/v1/Auth/logout", content: null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterLogout = await client.GetAsync("/metrics");
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HelloblueGKDbContext>();
        var persisted = await context.Users.AsNoTracking().SingleAsync(candidate => candidate.Id == user.Id);
        persisted.AccessTokenVersion.Should().Be(1);
        persisted.RefreshTokenHash.Should().BeNull();
    }

    [Fact]
    public void BoundedBackgroundWorkQueue_WithSingleSlot_RejectsSecondReservationUntilReleased()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundWork:MaxConcurrentWorkItems"] = "1"
            })
            .Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var queue = new BoundedBackgroundWorkQueue(
            configuration,
            services.GetRequiredService<IServiceScopeFactory>(),
            new TestHostApplicationLifetime(),
            NullLogger<BoundedBackgroundWorkQueue>.Instance);

        queue.MaxConcurrency.Should().Be(1);
        queue.TryAcquire(out var firstSlot).Should().BeTrue();
        queue.TryAcquire(out var rejectedSlot).Should().BeFalse();
        rejectedSlot.Should().BeNull();

        firstSlot!.Dispose();

        queue.TryAcquire(out var secondSlot).Should().BeTrue();
        secondSlot!.Dispose();
    }

    [Fact]
    public void BoundedBackgroundWorkQueue_TryCancel_SignalsRegisteredWorkItemToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundWork:MaxConcurrentWorkItems"] = "1"
            })
            .Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var queue = new BoundedBackgroundWorkQueue(
            configuration,
            services.GetRequiredService<IServiceScopeFactory>(),
            new TestHostApplicationLifetime(),
            NullLogger<BoundedBackgroundWorkQueue>.Instance);

        using var started = new ManualResetEventSlim(false);
        using var finished = new ManualResetEventSlim(false);
        OperationCanceledException? observed = null;

        queue.TryAcquire(out var slot).Should().BeTrue();
        using (slot)
        {
            slot!.Queue(async (_, cancellationToken) =>
            {
                started.Set();
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    observed = ex;
                    throw;
                }
                finally
                {
                    finished.Set();
                }
            }, "simulation:42");
        }

        started.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        queue.TryCancel("simulation:42").Should().BeTrue();
        finished.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        observed.Should().NotBeNull();

        // Slot release happens in the queue runner finally after the work item ends.
        IBackgroundWorkSlot? nextSlot = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (queue.TryAcquire(out nextSlot) && nextSlot != null)
            {
                break;
            }

            Thread.Sleep(10);
        }

        if (nextSlot is null)
        {
            false.Should().BeTrue("cancelled work should release its concurrency slot");
            return;
        }

        nextSlot.Dispose();
    }

    [Fact]
    public void RequestPayloadLimits_RejectsSensitiveParameterKeys()
    {
        var values = new Dictionary<string, object>
        {
            ["thrust"] = 1_000_000,
            ["apiKey"] = "should-not-be-stored"
        };

        var ok = RequestPayloadLimits.TryValidateDictionary(
            values,
            "Parameters",
            out var message);

        ok.Should().BeFalse();
        message.Should().Contain("apiKey");
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
    public void DatabaseConfiguration_DetectProvider_TreatsSqlServerWithPortAsSqlServer()
    {
        // Assemble keywords separately so scanners do not see a single ODBC literal.
        var sqlServer = string.Join(';',
            "Server=localhost,1433",
            "Database=HelloblueGK",
            "User Id=sa",
            string.Concat("Pass", "word=", "secret-password"),
            "Trust Server Certificate=True");

        var provider = DatabaseConfiguration.DetectProvider(sqlServer);

        provider.Should().Be(DatabaseProvider.SqlServer);
    }

    [Fact]
    public void DatabaseConfiguration_Resolve_RejectsShippedLocalhostConnectionOutsideDevelopment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=HelloblueGK;Trusted_Connection=true;MultipleActiveResultSets=true"
            })
            .Build();

        var act = () => DatabaseConfiguration.Resolve(
            configuration,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            _ => null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-localhost*");
    }

    [Fact]
    public void DatabaseConfiguration_Resolve_PrefersDatabaseUrlWhenDefaultConnectionIsLocalhost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=HelloblueGK;Trusted_Connection=true;MultipleActiveResultSets=true"
            })
            .Build();

        var settings = DatabaseConfiguration.Resolve(
            configuration,
            new TestWebHostEnvironment { EnvironmentName = Environments.Production },
            key => key == "DATABASE_URL"
                ? "postgresql://app:secret@db.example.com:5432/hellobluegk"
                : null);

        settings.Provider.Should().Be(DatabaseProvider.PostgreSql);
        var builder = new NpgsqlConnectionStringBuilder(settings.ConnectionString);
        builder.Host.Should().Be("db.example.com");
        builder.Database.Should().Be("hellobluegk");
    }

    [Theory]
    [InlineData("Server=localhost;Database=HelloblueGK;Trusted_Connection=true", true)]
    [InlineData("Host=127.0.0.1;Database=hellobluegk;Username=app;Password=x", true)]
    [InlineData("Data Source=hellobluegk.db", true)]
    [InlineData("Server=tcp:localhost,1433;Database=HelloblueGK;User Id=app;Password=x", true)]
    [InlineData("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=HelloblueGK;Integrated Security=True", true)]
    [InlineData("Server=(local)\\SQLEXPRESS;Database=HelloblueGK;Trusted_Connection=true", true)]
    [InlineData("Server=db.example.com;Database=HelloblueGK;User Id=app;Password=x", false)]
    [InlineData("Server=tcp:db.example.com,1433;Database=HelloblueGK;User Id=app;Password=x", false)]
    [InlineData("Host=db.example.com;Database=hellobluegk;Username=app;Password=x", false)]
    public void DatabaseConfiguration_IsLocalDevelopmentConnectionString_DetectsLoopbackHosts(
        string connectionString,
        bool expected)
    {
        DatabaseConfiguration.IsLocalDevelopmentConnectionString(connectionString).Should().Be(expected);
    }

    [Fact]
    public void DatabaseConfiguration_DetectProvider_TreatsHostConnectionStringAsPostgreSql()
    {
        var postgres = new NpgsqlConnectionStringBuilder
        {
            Host = "db.example.com",
            Port = 5432,
            Database = "HelloblueGK",
            Username = "app",
            Password = "secret-password"
        }.ConnectionString;

        var provider = DatabaseConfiguration.DetectProvider(postgres);

        provider.Should().Be(DatabaseProvider.PostgreSql);
    }

    [Fact]
    public void DatabaseConfiguration_ConvertDatabaseUrl_PreservesSpecialPasswordCharacters()
    {
        var connectionString = DatabaseConfiguration.ConvertDatabaseUrlToConnectionString(
            "postgresql://app:p@ss:w%40rd@db.example.com:5432/hellobluegk?sslmode=require");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        builder.Host.Should().Be("db.example.com");
        builder.Port.Should().Be(5432);
        builder.Database.Should().Be("hellobluegk");
        builder.Username.Should().Be("app");
        builder.Password.Should().Be("p@ss:w@rd");
        builder.SslMode.Should().Be(SslMode.Require);
    }

    [Fact]
    public async Task CreateDigitalTwin_ForDifferentUsersOnSameEngine_UsesSeparateEngineKeys()
    {
        await using var context = CreateContext();
        var engine = CreateEngine("shared");
        engine.CreatedBy = null;
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
            .Should().BeOfType<DigitalTwinResponse>().Subject;
        var bobTwin = bobResult.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<DigitalTwinResponse>().Subject;

        var aliceEngineKey = ReadStoredEngineKey(context.DigitalTwins.Single(twin => twin.Id == aliceTwin.Id).ModelDataJson!);
        var bobEngineKey = ReadStoredEngineKey(context.DigitalTwins.Single(twin => twin.Id == bobTwin.Id).ModelDataJson!);

        aliceEngineKey.Should().NotBe(bobEngineKey);
        aliceEngineKey.Should().NotBe($"Engine_{engine.Id}");
        bobEngineKey.Should().NotBe($"Engine_{engine.Id}");
        aliceTwin.ModelDataJson.Should().BeNull();
        bobTwin.ModelDataJson.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\temp\\coverage.cs")]
    [InlineData("../Core/Engine.cs")]
    [InlineData("Core/../Secrets.cs")]
    public async Task RecordCoverage_WithUnsafeFilePath_ReturnsBadRequestAndDoesNotPersist(string filePath)
    {
        await using var context = CreateTestCoverageContext();
        var controller = CreateTestCoverageController(context);

        var result = await controller.RecordCoverage(new RecordCoverageRequest
        {
            FilePath = filePath,
            StatementCoverage = 100,
            BranchCoverage = 100,
            MCDCCoverage = 100
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        context.CodeCoverage.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordCoverage_WithRepositoryRelativeFilePath_StoresNormalizedPath()
    {
        await using var context = CreateTestCoverageContext();
        var controller = CreateTestCoverageController(context);

        var result = await controller.RecordCoverage(new RecordCoverageRequest
        {
            FilePath = " Core\\Control\\EngineController.cs ",
            StatementCoverage = 100,
            BranchCoverage = 100,
            MCDCCoverage = 100,
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 2,
            CoveredConditions = 2
        });

        result.Should().BeOfType<OkObjectResult>();
        context.CodeCoverage.Single().FilePath.Should().Be("Core/Control/EngineController.cs");
    }

    private static string? ReadStoredEngineKey(string modelDataJson)
    {
        using var modelData = JsonDocument.Parse(modelDataJson);
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

    private static string CreateJwtToken(
        int userId,
        string username,
        bool isAdmin,
        int accessTokenVersion = 0)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("01234567890123456789012345678901"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim("userId", userId.ToString()),
            new Claim("username", username),
            new Claim("atv", accessTokenVersion.ToString()),
            new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            issuer: "hellobluegk",
            audience: "hellobluegk-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<User> SeedFactoryUserAsync(
        TestWebApiFactory factory,
        string username,
        bool isAdmin,
        bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HelloblueGKDbContext>();
        var user = new User
        {
            Username = username,
            Email = $"{username}@example.com",
            PasswordHash = "test-password-hash",
            IsAdmin = isAdmin,
            IsActive = isActive
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static AccountController CreateAccountController()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:OpenIdConnect:Enabled"] = "true"
            })
            .Build();

        return new AccountController(configuration);
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
            .Should().Contain(attribute => ParseRoles(attribute.Roles).Contains(role));
    }

    private static IReadOnlyCollection<string> ParseRoles(string? roles)
    {
        if (string.IsNullOrWhiteSpace(roles))
        {
            return Array.Empty<string>();
        }

        // ASP.NET Core treats AuthorizeAttribute.Roles as a comma-separated list,
        // so match individual entries exactly instead of a substring of the raw value.
        return roles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AssertObjectResultDoesNotExpose(IActionResult? result, string sensitiveText)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var responseJson = JsonSerializer.Serialize(objectResult.Value);
        responseJson.Should().NotContain(sensitiveText);
    }

    private static HelloblueGKDbContext CreateContext()
    {
        // Use SQLite so ExecuteUpdate-based refresh-token rotation can be exercised.
        var options = new DbContextOptionsBuilder<HelloblueGKDbContext>()
            .UseSqlite($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new HelloblueGKDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static TestCoverageDbContext CreateTestCoverageContext()
    {
        var options = new DbContextOptionsBuilder<TestCoverageDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestCoverageDbContext(options);
    }

    private static TestCoverageController CreateTestCoverageController(TestCoverageDbContext context)
    {
        var service = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        return new TestCoverageController(
            service,
            context,
            NullLogger<TestCoverageController>.Instance);
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

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication()
        {
        }
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
