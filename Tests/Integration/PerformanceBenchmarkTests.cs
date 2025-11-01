using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;
using System.Diagnostics;

namespace HelloblueGK.Tests.Integration;

/// <summary>
/// Performance benchmark tests for critical system operations
/// Ensures system meets performance requirements under load
/// </summary>
public class PerformanceBenchmarkTests
{
    private readonly Mock<ILogger<PerformanceMonitoringService>> _mockLogger;
    private readonly PerformanceMonitoringService _performanceService;
    private readonly Mock<ILogger<RateLimitingService>> _mockRateLimitLogger;
    private readonly RateLimitingService _rateLimitingService;

    public PerformanceBenchmarkTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMonitoringService>>();
        _performanceService = new PerformanceMonitoringService(_mockLogger.Object);
        
        _mockRateLimitLogger = new Mock<ILogger<RateLimitingService>>();
        _rateLimitingService = new RateLimitingService(_mockRateLimitLogger.Object);
    }

    [Fact]
    public async Task PerformanceMonitoring_RecordMetric_ShouldBeFast()
    {
        // Arrange
        const int iterations = 10000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _performanceService.RecordMetric($"BenchmarkMetric_{i}", i * 1.5, "Benchmark");
        }
        stopwatch.Stop();

        // Assert
        var averageTimePerOperation = stopwatch.Elapsed.TotalMilliseconds / iterations;
        averageTimePerOperation.Should().BeLessThan(1.0, "Metric recording should be very fast");
        
        // Verify metrics were recorded
        var metric = _performanceService.GetMetric($"BenchmarkMetric_{iterations - 1}");
        metric.Should().NotBeNull();
        metric!.Value.Should().Be((iterations - 1) * 1.5);
    }

    [Fact]
    public async Task PerformanceMonitoring_GenerateReport_ShouldCompleteQuickly()
    {
        // Arrange
        // Pre-populate some metrics
        for (int i = 0; i < 1000; i++)
        {
            _performanceService.RecordMetric($"ReportMetric_{i}", i, "ReportTest");
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var report = await _performanceService.GeneratePerformanceReportAsync();
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(1000, "Report generation should complete within 1 second");
        report.Should().NotBeNull();
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RateLimiting_CheckRateLimit_ShouldBeFast()
    {
        // Arrange
        const int iterations = 5000;
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 10000,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            await _rateLimitingService.CheckRateLimitAsync($"BenchmarkClient_{i}", policy);
        }
        stopwatch.Stop();

        // Assert
        var averageTimePerOperation = stopwatch.Elapsed.TotalMilliseconds / iterations;
        averageTimePerOperation.Should().BeLessThan(5.0, "Rate limit checking should be fast");
    }

    [Fact]
    public async Task RateLimiting_GenerateReport_ShouldCompleteQuickly()
    {
        // Arrange
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 100,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        // Create some rate limit activity
        for (int i = 0; i < 100; i++)
        {
            await _rateLimitingService.CheckRateLimitAsync($"ReportClient_{i}", policy);
        }

        var stopwatch = Stopwatch.StartNew();

        // Act
        var report = await _rateLimitingService.GenerateReportAsync();
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(500, "Rate limit report should complete within 500ms");
        report.Should().NotBeNull();
        report.TotalActiveBuckets.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MemoryUsage_ShouldNotGrowExcessively()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);
        
        // Act - Perform many operations
        for (int i = 0; i < 10000; i++)
        {
            _performanceService.RecordMetric($"MemoryTest_{i}", i, "MemoryTest");
            
            if (i % 1000 == 0)
            {
                await _performanceService.GeneratePerformanceReportAsync();
            }
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory increase should be less than 50MB"); // 50MB limit
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldNotCauseDeadlocks()
    {
        // Arrange
        const int concurrentTasks = 50;
        const int operationsPerTask = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < concurrentTasks; i++)
        {
            var taskIndex = i;
            var task = Task.Run(async () =>
            {
                for (int j = 0; j < operationsPerTask; j++)
                {
                    _performanceService.RecordMetric($"Concurrent_{taskIndex}_{j}", j, "ConcurrentTest");
                    
                    if (j % 10 == 0)
                    {
                        await _rateLimitingService.CheckRateLimitAsync($"ConcurrentClient_{taskIndex}", new RateLimitPolicy
                        {
                            RequestsPerWindow = 1000,
                            WindowSize = TimeSpan.FromMinutes(1),
                            Algorithm = RateLimitAlgorithm.SlidingWindow
                        });
                    }
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        // Assert
        tasks.Should().AllSatisfy(task => task.Status.Should().Be(TaskStatus.RanToCompletion));
    }

    [Fact]
    public async Task LargeDataSet_ShouldHandleEfficiently()
    {
        // Arrange
        const int largeDataSetSize = 100000;
        var stopwatch = Stopwatch.StartNew();

        // Act - Create large dataset
        for (int i = 0; i < largeDataSetSize; i++)
        {
            _performanceService.RecordMetric($"LargeData_{i}", i % 1000, "LargeDataSet");
        }

        // Get snapshot
        var snapshot = await _performanceService.GetCurrentSnapshotAsync();
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(5000, "Large dataset processing should complete within 5 seconds");
        snapshot.Should().NotBeNull();
        snapshot.TopMetrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StressTest_ShouldMaintainPerformance()
    {
        // Arrange
        const int stressIterations = 50000;
        var policy = new RateLimitPolicy
        {
            RequestsPerWindow = 100000,
            WindowSize = TimeSpan.FromMinutes(1),
            Algorithm = RateLimitAlgorithm.SlidingWindow
        };

        var stopwatch = Stopwatch.StartNew();

        // Act - Stress test with mixed operations
        for (int i = 0; i < stressIterations; i++)
        {
            _performanceService.RecordMetric($"Stress_{i}", i, "StressTest");
            
            if (i % 100 == 0)
            {
                await _rateLimitingService.CheckRateLimitAsync($"StressClient_{i % 1000}", policy);
            }
        }
        stopwatch.Stop();

        // Assert
        var operationsPerSecond = stressIterations / stopwatch.Elapsed.TotalSeconds;
        operationsPerSecond.Should().BeGreaterThan(10000, "Should handle at least 10,000 operations per second");
    }

    [Fact]
    public async Task ResourceCleanup_ShouldNotLeakResources()
    {
        // Arrange
        var initialHandleCount = Process.GetCurrentProcess().HandleCount;
        
        // Act - Create and dispose many services
        for (int i = 0; i < 100; i++)
        {
            var mockLogger = new Mock<ILogger<PerformanceMonitoringService>>();
            var service = new PerformanceMonitoringService(mockLogger.Object);
            
            service.RecordMetric($"CleanupTest_{i}", i, "CleanupTest");
        }

        // Force cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalHandleCount = Process.GetCurrentProcess().HandleCount;
        var handleIncrease = finalHandleCount - initialHandleCount;

        // Assert
        handleIncrease.Should().BeLessThan(50, "Handle count should not increase significantly");
    }

    [Fact]
    public async Task BenchmarkResults_ShouldMeetPerformanceRequirements()
    {
        // Arrange
        var benchmarkResults = new Dictionary<string, TimeSpan>();
        
        // Benchmark metric recording
        var metricStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            _performanceService.RecordMetric($"Benchmark_{i}", i, "Benchmark");
        }
        metricStopwatch.Stop();
        benchmarkResults["MetricRecording"] = metricStopwatch.Elapsed;

        // Benchmark report generation
        var reportStopwatch = Stopwatch.StartNew();
        await _performanceService.GeneratePerformanceReportAsync();
        reportStopwatch.Stop();
        benchmarkResults["ReportGeneration"] = reportStopwatch.Elapsed;

        // Benchmark rate limiting
        var rateLimitStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            await _rateLimitingService.CheckRateLimitAsync($"Benchmark_{i}", new RateLimitPolicy
            {
                RequestsPerWindow = 1000,
                WindowSize = TimeSpan.FromMinutes(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow
            });
        }
        rateLimitStopwatch.Stop();
        benchmarkResults["RateLimitChecking"] = rateLimitStopwatch.Elapsed;

        // Assert performance requirements
        benchmarkResults["MetricRecording"].TotalMilliseconds.Should().BeLessThan(100, "Metric recording should be very fast");
        benchmarkResults["ReportGeneration"].TotalMilliseconds.Should().BeLessThan(1000, "Report generation should complete quickly");
        benchmarkResults["RateLimitChecking"].TotalMilliseconds.Should().BeLessThan(500, "Rate limit checking should be fast");

        // Log benchmark results
        foreach (var result in benchmarkResults)
        {
            Console.WriteLine($"{result.Key}: {result.Value.TotalMilliseconds:F2}ms");
        }
    }
}
