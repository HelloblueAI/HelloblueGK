using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class RateLimitingServiceTests
{
    private readonly Mock<ILogger<RateLimitingService>> _mockLogger;
    private readonly RateLimitingService _service;

    public RateLimitingServiceTests()
    {
        _mockLogger = new Mock<ILogger<RateLimitingService>>();
        _service = new RateLimitingService(_mockLogger.Object);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WithinLimits_ShouldAllow()
    {
        // Arrange
        var identifier = "test-client";
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 10,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        // Act
        var result = await _service.CheckRateLimitAsync(identifier, policy);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(9);
        result.TotalRequests.Should().Be(1);
    }

    [Fact]
    public async Task CheckRateLimitAsync_ExceedsLimits_ShouldBlock()
    {
        // Arrange
        var identifier = "test-client-2";
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 2,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        // Act - Make requests up to the limit
        var result1 = await _service.CheckRateLimitAsync(identifier, policy);
        var result2 = await _service.CheckRateLimitAsync(identifier, policy);
        var result3 = await _service.CheckRateLimitAsync(identifier, policy);

        // Assert
        result1.IsAllowed.Should().BeTrue();
        result2.IsAllowed.Should().BeTrue();
        result3.IsAllowed.Should().BeFalse();
        result3.RemainingRequests.Should().Be(0);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WithMaxRequests_ShouldWork()
    {
        // Arrange
        var identifier = "test-client-3";
        var maxRequests = 5;
        var window = TimeSpan.FromSeconds(60);

        // Act
        var result = await _service.CheckRateLimitAsync(identifier, maxRequests, window);

        // Assert
        result.Should().NotBeNull();
        result.IsAllowed.Should().BeTrue();
        result.RemainingRequests.Should().Be(4);
    }

    [Fact]
    public async Task GetRateLimitStatusAsync_ShouldReturnStatus()
    {
        // Arrange
        var identifier = "test-client-4";
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 10,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        await _service.CheckRateLimitAsync(identifier, policy);

        // Act
        var status = await _service.GetRateLimitStatusAsync(identifier);

        // Assert
        status.Should().NotBeNull();
        status.Identifier.Should().Be(identifier);
        status.IsActive.Should().BeTrue();
        status.RemainingRequests.Should().BeGreaterThanOrEqualTo(8).And.BeLessThanOrEqualTo(10);
        status.Policy.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateReportAsync_ShouldReturnReport()
    {
        // Arrange
        var identifier = "test-client-5";
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 5,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        // Make some requests
        for (int i = 0; i < 3; i++)
        {
            await _service.CheckRateLimitAsync(identifier, policy);
        }

        // Act
        var report = await _service.GenerateReportAsync();

        // Assert
        report.Should().NotBeNull();
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        report.TotalActiveBuckets.Should().BeGreaterThan(0);
        report.TotalBuckets.Should().BeGreaterThan(0);
        report.AllowedRequests.Should().BeGreaterThan(0);
        report.TopBlockedIdentifiers.Should().NotBeNull();
    }

    [Fact]
    public async Task ResetRateLimitAsync_ShouldResetLimits()
    {
        // Arrange
        var identifier = "test-client-6";
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 1,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        // Make a request to establish the bucket
        await _service.CheckRateLimitAsync(identifier, policy);
        
        // Verify it's blocked on second request
        var blockedResult = await _service.CheckRateLimitAsync(identifier, policy);
        blockedResult.IsAllowed.Should().BeFalse();

        // Act
        await _service.ResetRateLimitAsync(identifier);

        // Assert - Should be able to make another request
        var allowedResult = await _service.CheckRateLimitAsync(identifier, policy);
        allowedResult.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetAllRateLimitsAsync_ShouldResetAllLimits()
    {
        // Arrange
        var identifier1 = "test-client-7";
        var identifier2 = "test-client-8";
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 1,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        // Make requests to establish buckets
        await _service.CheckRateLimitAsync(identifier1, policy);
        await _service.CheckRateLimitAsync(identifier2, policy);

        // Act
        await _service.ResetAllRateLimitsAsync();

        // Assert - Both should be able to make requests again
        var result1 = await _service.CheckRateLimitAsync(identifier1, policy);
        var result2 = await _service.CheckRateLimitAsync(identifier2, policy);

        result1.IsAllowed.Should().BeTrue();
        result2.IsAllowed.Should().BeTrue();
    }
}
