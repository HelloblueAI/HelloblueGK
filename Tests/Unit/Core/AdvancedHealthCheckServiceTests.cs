using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class AdvancedHealthCheckServiceTests
{
    private readonly Mock<ILogger<AdvancedHealthCheckService>> _mockLogger;
    private readonly Mock<PerformanceMonitoringService> _mockPerformanceService;
    private readonly Mock<RateLimitingService> _mockRateLimitingService;
    private readonly Mock<ConfigurationValidationService> _mockConfigValidation;
    private readonly Mock<StructuredLoggingService> _mockStructuredLogging;
    private readonly AdvancedHealthCheckService _service;

    public AdvancedHealthCheckServiceTests()
    {
        _mockLogger = new Mock<ILogger<AdvancedHealthCheckService>>();
        _mockPerformanceService = new Mock<PerformanceMonitoringService>(Mock.Of<ILogger<PerformanceMonitoringService>>());
        _mockRateLimitingService = new Mock<RateLimitingService>(Mock.Of<ILogger<RateLimitingService>>());
        
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        _mockConfigValidation = new Mock<ConfigurationValidationService>(
            mockConfig.Object, 
            Mock.Of<ILogger<ConfigurationValidationService>>());
            
        _mockStructuredLogging = new Mock<StructuredLoggingService>(Mock.Of<ILogger<StructuredLoggingService>>());

        _service = new AdvancedHealthCheckService(
            _mockLogger.Object,
            _mockPerformanceService.Object,
            _mockRateLimitingService.Object,
            _mockConfigValidation.Object,
            _mockStructuredLogging.Object);
    }

    [Fact]
    public async Task GetSystemHealthAsync_ShouldReturnHealthReport()
    {
        // Act
        var report = await _service.GetSystemHealthAsync();

        // Assert
        report.Should().NotBeNull();
        report.OverallStatus.Should().BeOneOf(
            AdvancedHealthStatus.Healthy,
            AdvancedHealthStatus.Degraded,
            AdvancedHealthStatus.Unhealthy);
        report.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetSystemHealthAsync_ShouldIncludeAllComponents()
    {
        // Act
        var report = await _service.GetSystemHealthAsync();

        // Assert
        report.SystemResources.Should().NotBeNull();
        report.ApplicationHealth.Should().NotBeNull();
        report.ExternalDependencies.Should().NotBeNull();
        report.PerformanceHealth.Should().NotBeNull();
        report.SecurityHealth.Should().NotBeNull();
        report.ConfigurationHealth.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSystemHealthAsync_ShouldHaveValidMetrics()
    {
        // Act
        var report = await _service.GetSystemHealthAsync();

        // Assert
        report.SystemResources.Metrics.Should().NotBeEmpty();
        report.ApplicationHealth.Metrics.Should().NotBeEmpty();
        report.PerformanceHealth.Metrics.Should().NotBeEmpty();
    }
}

