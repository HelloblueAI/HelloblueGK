using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using HB_NLP_Research_Lab.Core;

namespace HelloblueGK.Tests.Unit.Core;

public class PerformanceMonitoringServiceTests
{
    private readonly Mock<ILogger<PerformanceMonitoringService>> _mockLogger;
    private readonly PerformanceMonitoringService _service;

    public PerformanceMonitoringServiceTests()
    {
        _mockLogger = new Mock<ILogger<PerformanceMonitoringService>>();
        _service = new PerformanceMonitoringService(_mockLogger.Object);
    }

    [Fact]
    public async Task RecordMetric_ShouldStoreMetricSuccessfully()
    {
        // Arrange
        var metricName = "TestMetric";
        var metricValue = 42.5;
        var category = "TestCategory";

        // Act
        _service.RecordMetric(metricName, metricValue, category);

        // Assert
        var metric = _service.GetMetric(metricName);
        metric.Should().NotBeNull();
        metric!.Name.Should().Be(metricName);
        metric.Value.Should().Be(metricValue);
        metric.Category.Should().Be(category);
        metric.Count.Should().Be(1);
    }

    [Fact]
    public async Task RecordExecutionTime_ShouldRecordDurationAndThroughput()
    {
        // Arrange
        var operationName = "TestOperation";
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        _service.RecordExecutionTime(operationName, duration);

        // Assert
        var durationMetric = _service.GetMetric($"{operationName}_Duration");
        var throughputMetric = _service.GetMetric($"{operationName}_Throughput");

        durationMetric.Should().NotBeNull();
        durationMetric!.Value.Should().Be(100.0);

        throughputMetric.Should().NotBeNull();
        throughputMetric!.Value.Should().Be(10.0); // 1 / 0.1 seconds = 10 ops/sec
    }

    [Fact]
    public async Task GetCurrentSnapshotAsync_ShouldReturnValidSnapshot()
    {
        // Act
        var snapshot = await _service.GetCurrentSnapshotAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        snapshot.SystemMetrics.Should().NotBeNull();
        snapshot.TopMetrics.Should().NotBeNull();
        snapshot.ActiveOperations.Should().NotBeNull();
    }

    [Fact]
    public async Task GeneratePerformanceReportAsync_ShouldReturnValidReport()
    {
        // Act
        var report = await _service.GeneratePerformanceReportAsync();

        // Assert
        report.Should().NotBeNull();
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        report.SystemMetrics.Should().NotBeNull();
        report.ApplicationMetrics.Should().NotBeNull();
        report.PerformanceTrends.Should().NotBeNull();
        report.Recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMetricsByCategory_ShouldReturnCorrectMetrics()
    {
        // Arrange
        _service.RecordMetric("Metric1", 10, "CategoryA");
        _service.RecordMetric("Metric2", 20, "CategoryA");
        _service.RecordMetric("Metric3", 30, "CategoryB");

        // Act
        var categoryAMetrics = _service.GetMetricsByCategory("CategoryA");
        var categoryBMetrics = _service.GetMetricsByCategory("CategoryB");

        // Assert
        categoryAMetrics.Should().HaveCount(2);
        categoryBMetrics.Should().HaveCount(1);
        
        categoryAMetrics.All(m => m.Category == "CategoryA").Should().BeTrue();
        categoryBMetrics.All(m => m.Category == "CategoryB").Should().BeTrue();
    }

    [Fact]
    public async Task GetTrendAnalysis_ShouldReturnTrendData()
    {
        // Arrange
        var metricName = "TrendTestMetric";
        
        // Record some historical data
        for (int i = 0; i < 10; i++)
        {
            _service.RecordMetric(metricName, i * 10, "TrendTest");
            await Task.Delay(10); // Small delay to create time separation
        }

        // Act
        var trends = _service.GetTrendAnalysis(metricName, TimeSpan.FromMinutes(1));

        // Assert
        trends.Should().NotBeEmpty();
        var trend = trends.First();
        trend.MetricName.Should().Be(metricName);
        trend.SampleCount.Should().BeGreaterThan(0);
    }
}
