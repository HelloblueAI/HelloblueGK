using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;
using System.Diagnostics;

namespace HelloblueGK.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the complete system
/// Tests the interaction between all system components
/// </summary>
public class SystemIntegrationTests
{
    private readonly Mock<ILogger<PerformanceMonitoringService>> _mockPerfLogger;
    private readonly Mock<ILogger<RateLimitingService>> _mockRateLimitLogger;
    private readonly Mock<ILogger<ConfigurationValidationService>> _mockConfigLogger;
    private readonly Mock<ILogger<AdvancedHealthCheckService>> _mockHealthLogger;
    private readonly Mock<ILogger<StructuredLoggingService>> _mockStructuredLogger;

    private readonly PerformanceMonitoringService _performanceService;
    private readonly RateLimitingService _rateLimitingService;
    private readonly ConfigurationValidationService _configValidation;
    private readonly StructuredLoggingService _structuredLogging;
    private readonly AdvancedHealthCheckService _healthCheckService;

    public SystemIntegrationTests()
    {
        _mockPerfLogger = new Mock<ILogger<PerformanceMonitoringService>>();
        _mockRateLimitLogger = new Mock<ILogger<RateLimitingService>>();
        _mockConfigLogger = new Mock<ILogger<ConfigurationValidationService>>();
        _mockHealthLogger = new Mock<ILogger<AdvancedHealthCheckService>>();
        _mockStructuredLogger = new Mock<ILogger<StructuredLoggingService>>();

        _performanceService = new PerformanceMonitoringService(_mockPerfLogger.Object);
        _rateLimitingService = new RateLimitingService(_mockRateLimitLogger.Object);
        _structuredLogging = new StructuredLoggingService(_mockStructuredLogger.Object);
        
        // Create a mock configuration for testing
        var configuration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        _configValidation = new ConfigurationValidationService(configuration.Object, _mockConfigLogger.Object);
        
        _healthCheckService = new AdvancedHealthCheckService(
            _mockHealthLogger.Object,
            _performanceService,
            _rateLimitingService,
            _configValidation,
            _structuredLogging);
    }

    [Fact]
    public async Task FullSystemWorkflow_ShouldWorkEndToEnd()
    {
        // Arrange
        var engineId = "IntegrationTestEngine_001";
        var operationStartTime = DateTime.UtcNow;

        // Act - Simulate a complete system workflow
        
        // 1. Record performance metrics
        _performanceService.RecordMetric("Engine_Thrust", 1500000, "Engine");
        _performanceService.RecordMetric("Engine_Efficiency", 0.89, "Engine");
        _performanceService.RecordExecutionTime("Engine_Simulation", TimeSpan.FromMilliseconds(250));

        // 2. Check rate limiting
        var rateLimitResult = await _rateLimitingService.CheckRateLimitAsync("IntegrationTestClient", new RateLimitPolicy
        {
            RequestsPerWindow = 100,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        });

        // 3. Log structured events
        _structuredLogging.LogEngineOperation("Simulation", engineId, new { Thrust = 1500000, Efficiency = 0.89 });
        _structuredLogging.LogPhysicsSimulation("CFD", engineId, new { Convergence = 0.99, Iterations = 150 }, TimeSpan.FromSeconds(2.5));

        // 4. Generate performance report
        var performanceReport = await _performanceService.GeneratePerformanceReportAsync();

        // 5. Generate rate limiting report
        var rateLimitReport = await _rateLimitingService.GenerateReportAsync();

        // 6. Run health check
        var healthReport = await _healthCheckService.GetSystemHealthAsync();

        // 7. Validate configuration
        var configValidation = await _configValidation.ValidateConfigurationAsync();

        var operationEndTime = DateTime.UtcNow;
        var totalOperationTime = operationEndTime - operationStartTime;

        // Assert
        rateLimitResult.IsAllowed.Should().BeTrue("Rate limiting should allow the request");
        
        performanceReport.Should().NotBeNull("Performance report should be generated");
        performanceReport.SystemMetrics.Should().NotBeNull("System metrics should be available");
        
        rateLimitReport.Should().NotBeNull("Rate limit report should be generated");
        rateLimitReport.TotalActiveBuckets.Should().BeGreaterThan(0, "Should have active rate limit buckets");
        
        healthReport.Should().NotBeNull("Health report should be generated");
        healthReport.OverallStatus.Should().BeOneOf(AdvancedHealthStatus.Healthy, AdvancedHealthStatus.Degraded, "System should be healthy or degraded");
        
        configValidation.Should().NotBeNull("Configuration validation should complete");
        
        totalOperationTime.TotalSeconds.Should().BeLessThan(10, "Full workflow should complete within 10 seconds");
    }

    [Fact]
    public async Task PerformanceMonitoring_WithRateLimiting_ShouldWorkTogether()
    {
        // Arrange
        const int iterations = 100;
        var performanceMetrics = new List<double>();
        var rateLimitResults = new List<bool>();

        // Act - Simulate concurrent performance monitoring and rate limiting
        for (int i = 0; i < iterations; i++)
        {
            // Record performance metrics
            var metricValue = Random.Shared.NextDouble() * 1000;
            _performanceService.RecordMetric($"ConcurrentMetric_{i}", metricValue, "ConcurrentTest");
            performanceMetrics.Add(metricValue);

            // Check rate limiting
            var rateLimitResult = await _rateLimitingService.CheckRateLimitAsync($"ConcurrentClient_{i % 10}", new RateLimitPolicy
            {
                RequestsPerWindow = 50,
                WindowSize = TimeSpan.FromMinutes(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow
            });
            rateLimitResults.Add(rateLimitResult.IsAllowed);
        }

        // Generate reports
        var performanceReport = await _performanceService.GeneratePerformanceReportAsync();
        var rateLimitReport = await _rateLimitingService.GenerateReportAsync();

        // Assert
        performanceMetrics.Should().HaveCount(iterations, "All performance metrics should be recorded");
        rateLimitResults.Should().HaveCount(iterations, "All rate limit checks should complete");
        
        performanceReport.Should().NotBeNull("Performance report should be generated");
        rateLimitReport.Should().NotBeNull("Rate limit report should be generated");
        
        // Some rate limit checks should be blocked due to limits
        rateLimitResults.Should().Contain(false, "Some requests should be rate limited");
    }

    [Fact]
    public async Task HealthCheck_WithAllComponents_ShouldProvideComprehensiveStatus()
    {
        // Act
        var healthReport = await _healthCheckService.GetSystemHealthAsync();

        // Assert
        healthReport.Should().NotBeNull("Health report should be generated");
        healthReport.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        
        // Check all components are included
        healthReport.SystemResources.Should().NotBeNull("System resources should be checked");
        healthReport.ApplicationHealth.Should().NotBeNull("Application health should be checked");
        healthReport.ExternalDependencies.Should().NotBeNull("External dependencies should be checked");
        healthReport.PerformanceHealth.Should().NotBeNull("Performance health should be checked");
        healthReport.SecurityHealth.Should().NotBeNull("Security health should be checked");
        healthReport.ConfigurationHealth.Should().NotBeNull("Configuration health should be checked");
        
        // Check metrics are populated
        healthReport.SystemResources.Metrics.Should().NotBeEmpty("System resources should have metrics");
        healthReport.ApplicationHealth.Metrics.Should().NotBeEmpty("Application health should have metrics");
        healthReport.PerformanceHealth.Metrics.Should().NotBeEmpty("Performance health should have metrics");
        healthReport.SecurityHealth.Metrics.Should().NotBeEmpty("Security health should have metrics");
    }

    [Fact]
    public async Task StructuredLogging_WithPerformanceMonitoring_ShouldCorrelateData()
    {
        // Arrange
        var engineId = "CorrelationTestEngine_001";
        var operationId = Guid.NewGuid().ToString();

        // Act
        using var scope = _structuredLogging.BeginOperationScope("EngineSimulation", operationId);
        
        _structuredLogging.LogEngineOperation("StartSimulation", engineId, new { Parameters = "TestParams" });
        
        var stopwatch = Stopwatch.StartNew();
        _performanceService.RecordMetric("Simulation_StartTime", stopwatch.ElapsedMilliseconds, "Simulation");
        
        // Simulate some work
        await Task.Delay(100);
        
        _performanceService.RecordMetric("Simulation_EndTime", stopwatch.ElapsedMilliseconds, "Simulation");
        _structuredLogging.LogEngineOperation("EndSimulation", engineId, new { Duration = stopwatch.ElapsedMilliseconds });
        
        _structuredLogging.LogOperationComplete("EngineSimulation", operationId, stopwatch.Elapsed, true, new { Success = true });

        // Generate reports
        var performanceReport = await _performanceService.GeneratePerformanceReportAsync();

        // Assert
        performanceReport.Should().NotBeNull("Performance report should be generated");
        performanceReport.ApplicationMetrics.TotalMetrics.Should().BeGreaterThan(0, "Should have recorded metrics");
    }

    [Fact]
    public async Task ErrorHandling_AcrossComponents_ShouldBeResilient()
    {
        // Arrange
        var errorCount = 0;
        var successCount = 0;

        // Act - Simulate various error conditions
        for (int i = 0; i < 50; i++)
        {
            try
            {
                // Normal operations
                _performanceService.RecordMetric($"ErrorTest_{i}", i, "ErrorTest");
                
                var rateLimitResult = await _rateLimitingService.CheckRateLimitAsync($"ErrorTestClient_{i}", new RateLimitPolicy
                {
                    RequestsPerWindow = 10,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                });
                
                _structuredLogging.LogEngineOperation("TestOperation", $"Engine_{i}", new { TestData = i });
                
                successCount++;
            }
            catch (Exception)
            {
                errorCount++;
            }
        }

        // Generate reports even after errors
        var performanceReport = await _performanceService.GeneratePerformanceReportAsync();
        var rateLimitReport = await _rateLimitingService.GenerateReportAsync();
        var healthReport = await _healthCheckService.GetSystemHealthAsync();

        // Assert
        successCount.Should().BeGreaterThan(0, "Some operations should succeed");
        performanceReport.Should().NotBeNull("Performance report should be generated even after errors");
        rateLimitReport.Should().NotBeNull("Rate limit report should be generated even after errors");
        healthReport.Should().NotBeNull("Health report should be generated even after errors");
    }

    [Fact]
    public async Task LoadTest_WithAllComponents_ShouldMaintainPerformance()
    {
        // Arrange
        const int loadTestIterations = 1000;
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task>();

        // Act - Simulate load across all components
        for (int i = 0; i < loadTestIterations; i++)
        {
            var iteration = i;
            var task = Task.Run(async () =>
            {
                // Performance monitoring
                _performanceService.RecordMetric($"LoadTest_{iteration}", iteration, "LoadTest");
                
                // Rate limiting
                await _rateLimitingService.CheckRateLimitAsync($"LoadTestClient_{iteration % 100}", new RateLimitPolicy
                {
                    RequestsPerWindow = 1000,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                });
                
                // Structured logging
                _structuredLogging.LogEngineOperation("LoadTestOperation", $"Engine_{iteration}", new { LoadTestData = iteration });
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Generate reports under load
        var performanceReport = await _performanceService.GeneratePerformanceReportAsync();
        var rateLimitReport = await _rateLimitingService.GenerateReportAsync();
        var healthReport = await _healthCheckService.GetSystemHealthAsync();

        // Assert
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(30, "Load test should complete within 30 seconds");
        
        performanceReport.Should().NotBeNull("Performance report should be generated under load");
        rateLimitReport.Should().NotBeNull("Rate limit report should be generated under load");
        healthReport.Should().NotBeNull("Health report should be generated under load");
        
        // Check that metrics were recorded
        performanceReport.ApplicationMetrics.TotalMetrics.Should().BeGreaterThan(0, "Should have recorded metrics under load");
        rateLimitReport.TotalActiveBuckets.Should().BeGreaterThan(0, "Should have active rate limit buckets under load");
    }

    [Fact]
    public async Task SystemRecovery_AfterErrors_ShouldWorkCorrectly()
    {
        // Arrange
        var initialHealth = await _healthCheckService.GetSystemHealthAsync();

        // Act - Simulate system stress and recovery
        for (int i = 0; i < 100; i++)
        {
            try
            {
                _performanceService.RecordMetric($"RecoveryTest_{i}", i, "RecoveryTest");
                await _rateLimitingService.CheckRateLimitAsync($"RecoveryClient_{i}", new RateLimitPolicy
                {
                    RequestsPerWindow = 10,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                });
            }
            catch
            {
                // Ignore errors during stress test
            }
        }

        // Wait for system to stabilize
        await Task.Delay(1000);

        // Test system recovery
        var recoveryHealth = await _healthCheckService.GetSystemHealthAsync();
        var performanceReport = await _performanceService.GeneratePerformanceReportAsync();
        var rateLimitReport = await _rateLimitingService.GenerateReportAsync();

        // Assert
        recoveryHealth.Should().NotBeNull("System should recover and provide health status");
        performanceReport.Should().NotBeNull("Performance monitoring should recover");
        rateLimitReport.Should().NotBeNull("Rate limiting should recover");
        
        // System should be functional after recovery
        _performanceService.RecordMetric("RecoveryTest", 100, "RecoveryTest");
        var rateLimitResult = await _rateLimitingService.CheckRateLimitAsync("RecoveryTestClient", new RateLimitPolicy
        {
            RequestsPerWindow = 100,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        });
        
        rateLimitResult.IsAllowed.Should().BeTrue("System should be functional after recovery");
    }
}
