using HB_NLP_Research_Lab.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace HelloblueGK.Tests.Unit.Core;

public class RateLimitingMiddlewareSecurityTests
{
    [Fact]
    public async Task InvokeAsync_WhenRateLimiterFailsForApiEndpoint_ShouldFailClosed()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(
            next,
            NullLogger<RateLimitingMiddleware>.Instance,
            new ThrowingRateLimitingService());

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/simulations";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        responseBody.Should().Contain("Rate limiting unavailable");
        responseBody.Should().Contain("Rate limiting is temporarily unavailable");
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/account/login")]
    public async Task InvokeAsync_ForAuthenticationEntrypoints_ShouldUseAuthPolicy(string path)
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);

        // Act
        var allowedResult = await InvokeMiddlewareAsync(rateLimitingService, path, HttpMethods.Post);
        for (var i = 1; i < 10; i++)
        {
            allowedResult = await InvokeMiddlewareAsync(rateLimitingService, path, HttpMethods.Post);
        }

        var blockedResult = await InvokeMiddlewareAsync(rateLimitingService, path, HttpMethods.Post);

        // Assert
        allowedResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        allowedResult.NextCalled.Should().BeTrue();
        allowedResult.Headers["X-RateLimit-Limit"].Should().Be("10");
        blockedResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        blockedResult.NextCalled.Should().BeFalse();
        blockedResult.Headers.Should().ContainKey("Retry-After");
    }

    [Theory]
    [InlineData("/api/v1/simulations")]
    [InlineData("/api/v1/aioptimization")]
    [InlineData("/api/v1/launches")]
    [InlineData("/api/v1/launches/42/launch")]
    [InlineData("/api/v1/digitaltwin")]
    [InlineData("/api/v1/digitaltwin/42/learn")]
    [InlineData("/api/v1/digitaltwin/42/predict")]
    public async Task InvokeAsync_ForExpensiveMutationEndpoints_ShouldUseTightPolicy(string path)
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);

        // Act
        var allowedResult = await InvokeMiddlewareAsync(rateLimitingService, path, HttpMethods.Post);
        for (var i = 1; i < 10; i++)
        {
            allowedResult = await InvokeMiddlewareAsync(rateLimitingService, path, HttpMethods.Post);
        }

        var blockedResult = await InvokeMiddlewareAsync(rateLimitingService, path, HttpMethods.Post);

        // Assert
        allowedResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        allowedResult.NextCalled.Should().BeTrue();
        allowedResult.Headers["X-RateLimit-Limit"].Should().Be("10");
        blockedResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        blockedResult.NextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenClientHitsGeneralApiFirst_ShouldStillApplyAuthPolicyToAuthEndpoint()
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);

        // Act
        var generalApiResult = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/engines",
            HttpMethods.Get);

        var allowedAuthResult = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/auth/login",
            HttpMethods.Post);
        for (var i = 1; i < 10; i++)
        {
            allowedAuthResult = await InvokeMiddlewareAsync(
                rateLimitingService,
                "/api/v1/auth/login",
                HttpMethods.Post);
        }

        var blockedAuthResult = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/auth/login",
            HttpMethods.Post);

        // Assert
        generalApiResult.Headers["X-RateLimit-Limit"].Should().Be("200");
        allowedAuthResult.Headers["X-RateLimit-Limit"].Should().Be("10");
        blockedAuthResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        blockedAuthResult.NextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ForAuthenticationEntrypoints_ShouldLimitRepeatedUsernameAcrossDifferentIps()
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);
        const string requestBody = """{"username":"victim","password":"bad-password"}""";

        // Act
        MiddlewareInvocationResult allowedResult = default!;
        for (var i = 0; i < 5; i++)
        {
            allowedResult = await InvokeMiddlewareAsync(
                rateLimitingService,
                "/api/v1/auth/login",
                HttpMethods.Post,
                IPAddress.Parse($"203.0.113.{10 + i}"),
                requestBody);
        }

        var blockedResult = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/auth/login",
            HttpMethods.Post,
            IPAddress.Parse("203.0.113.99"),
            requestBody);

        // Assert
        allowedResult.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        allowedResult.NextCalled.Should().BeTrue();
        allowedResult.Headers["X-RateLimit-Limit"].Should().Be("5");
        blockedResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        blockedResult.NextCalled.Should().BeFalse();
        blockedResult.Headers["X-RateLimit-Limit"].Should().Be("5");
    }

    [Fact]
    public async Task InvokeAsync_ForAuthenticationEntrypoints_ShouldThrottleLastDuplicateUsernameValue()
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);
        const string requestBody = """{"username":"decoy","username":"victim","password":"bad-password"}""";

        // Act
        for (var i = 0; i < 5; i++)
        {
            await InvokeMiddlewareAsync(
                rateLimitingService,
                "/api/v1/auth/login",
                HttpMethods.Post,
                IPAddress.Parse($"203.0.113.{10 + i}"),
                requestBody);
        }

        var blockedResult = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/auth/login",
            HttpMethods.Post,
            IPAddress.Parse("203.0.113.99"),
            requestBody);

        // Assert
        blockedResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        blockedResult.NextCalled.Should().BeFalse();
        blockedResult.Headers["X-RateLimit-Limit"].Should().Be("5");
    }

    [Fact]
    public async Task InvokeAsync_WhenUsernameRateLimiterFails_ShouldFailClosed()
    {
        // Arrange
        const string requestBody = """{"username":"victim","password":"bad-password"}""";
        var middleware = new RateLimitingMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RateLimitingMiddleware>.Instance,
            new UsernameThrowingRateLimitingService());

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Request.Path = "/api/v1/auth/login";
        context.Request.Method = HttpMethods.Post;
        var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.ContentType = "application/json";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_ForAuthenticationEntrypoints_WithOversizedBody_ShouldRejectPayload()
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);
        var requestBody = $$"""{"username":"victim","password":"{{new string('x', 64 * 1024)}}"}""";

        // Act
        var result = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/auth/login",
            HttpMethods.Post,
            requestBody: requestBody);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        result.NextCalled.Should().BeFalse();
        result.ResponseBody.Should().Contain("Payload too large");
    }

    [Fact]
    public async Task InvokeAsync_ForAuthenticationEntrypoints_WithChunkedOversizedBody_ShouldRejectPayload()
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);
        var padding = new string('x', 64 * 1024);
        var requestBody = $$"""{"padding":"{{padding}}","username":"victim","password":"bad-password"}""";

        // Act
        var result = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/auth/login",
            HttpMethods.Post,
            requestBody: requestBody,
            includeContentLength: false);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        result.NextCalled.Should().BeFalse();
        result.ResponseBody.Should().Contain("Payload too large");
    }

    [Fact]
    public async Task InvokeAsync_ForAiOptimizationReadEndpoint_ShouldUseAiPolicy()
    {
        // Arrange
        using var rateLimitingService = new RateLimitingService(NullLogger<RateLimitingService>.Instance);

        // Act
        var result = await InvokeMiddlewareAsync(
            rateLimitingService,
            "/api/v1/aioptimization/42/status",
            HttpMethods.Get);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.NextCalled.Should().BeTrue();
        result.Headers["X-RateLimit-Limit"].Should().Be("50");
    }

    private static async Task<MiddlewareInvocationResult> InvokeMiddlewareAsync(
        RateLimitingService rateLimitingService,
        string path,
        string method,
        IPAddress? remoteIpAddress = null,
        string? requestBody = null,
        bool includeContentLength = true)
    {
        var nextCalled = false;
        RequestDelegate next = context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(
            next,
            NullLogger<RateLimitingMiddleware>.Instance,
            rateLimitingService);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIpAddress ?? IPAddress.Parse("203.0.113.10");
        context.Request.Path = path;
        context.Request.Method = method;
        if (requestBody != null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
            context.Request.Body = new MemoryStream(bodyBytes);
            if (includeContentLength)
            {
                context.Request.ContentLength = bodyBytes.Length;
            }

            context.Request.ContentType = "application/json";
        }
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var headers = context.Response.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString());

        return new MiddlewareInvocationResult(
            context.Response.StatusCode,
            nextCalled,
            headers,
            responseBody);
    }

    private sealed record MiddlewareInvocationResult(
        int StatusCode,
        bool NextCalled,
        IReadOnlyDictionary<string, string> Headers,
        string ResponseBody);

    private sealed class ThrowingRateLimitingService : RateLimitingService
    {
        public ThrowingRateLimitingService()
            : base(NullLogger<RateLimitingService>.Instance)
        {
        }

        public override Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitPolicy policy)
        {
            throw new InvalidOperationException("simulated limiter failure");
        }
    }

    private sealed class UsernameThrowingRateLimitingService : RateLimitingService
    {
        public UsernameThrowingRateLimitingService()
            : base(NullLogger<RateLimitingService>.Instance)
        {
        }

        public override Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitPolicy policy)
        {
            if (identifier.StartsWith("AuthUsername:", StringComparison.Ordinal))
            {
                throw new TimeoutException("simulated username limiter failure");
            }

            return base.CheckRateLimitAsync(identifier, policy);
        }
    }
}
