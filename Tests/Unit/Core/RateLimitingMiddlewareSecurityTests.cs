using HB_NLP_Research_Lab.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
}
