using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HB_NLP_Research_Lab.Core;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Performance monitoring and metrics API controller
    /// Provides real-time performance data and analytics
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("Performance")]
    public class PerformanceController : ControllerBase
    {
        private readonly ILogger<PerformanceController> _logger;
        private readonly PerformanceMonitoringService _performanceService;

        public PerformanceController(ILogger<PerformanceController> logger, PerformanceMonitoringService performanceService)
        {
            _logger = logger;
            _performanceService = performanceService;
        }

        /// <summary>
        /// Get current performance snapshot
        /// </summary>
        /// <returns>Current performance metrics</returns>
        [HttpGet("snapshot")]
        public async Task<ActionResult<PerformanceSnapshot>> GetSnapshot()
        {
            try
            {
                var snapshot = await _performanceService.GetCurrentSnapshotAsync();
                return Ok(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance snapshot");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get comprehensive performance report
        /// </summary>
        /// <returns>Detailed performance report</returns>
        [HttpGet("report")]
        public async Task<ActionResult<PerformanceReport>> GetReport()
        {
            try
            {
                var report = await _performanceService.GeneratePerformanceReportAsync();
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance report");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get specific metric by name
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <returns>Metric data</returns>
        [HttpGet("metric/{metricName}")]
        public ActionResult<PerformanceMetric> GetMetric(string metricName)
        {
            try
            {
                var metric = _performanceService.GetMetric(metricName);
                if (metric == null)
                {
                    return NotFound($"Metric '{metricName}' not found");
                }

                return Ok(metric);
            }
            catch (Exception ex)
            {
                var sanitizedMetricName = LogSanitizer.Sanitize(metricName);
                _logger.LogError(ex, "Error getting metric {MetricName}", sanitizedMetricName);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get metrics by category
        /// </summary>
        /// <param name="category">Category name</param>
        /// <returns>List of metrics in the category</returns>
        [HttpGet("metrics/category/{category}")]
        public ActionResult<IEnumerable<PerformanceMetric>> GetMetricsByCategory(string category)
        {
            try
            {
                var metrics = _performanceService.GetMetricsByCategory(category);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                var sanitizedCategory = LogSanitizer.Sanitize(category);
                _logger.LogError(ex, "Error getting metrics for category {Category}", sanitizedCategory);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get performance trend analysis for a specific metric
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="lookbackMinutes">Lookback period in minutes (default: 60)</param>
        /// <returns>Trend analysis</returns>
        [HttpGet("trend/{metricName}")]
        public ActionResult<IEnumerable<PerformanceTrend>> GetTrendAnalysis(string metricName, [FromQuery] int lookbackMinutes = 60)
        {
            try
            {
                var lookbackPeriod = TimeSpan.FromMinutes(lookbackMinutes);
                var trends = _performanceService.GetTrendAnalysis(metricName, lookbackPeriod);
                return Ok(trends);
            }
            catch (Exception ex)
            {
                var sanitizedMetricName = LogSanitizer.Sanitize(metricName);
                _logger.LogError(ex, "Error getting trend analysis for metric {MetricName}", sanitizedMetricName);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Record a custom performance metric
        /// </summary>
        /// <param name="request">Metric recording request</param>
        /// <returns>Success status</returns>
        [HttpPost("record")]
        public IActionResult RecordMetric([FromBody] RecordMetricRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest("Metric name is required");
                }

                _performanceService.RecordMetric(request.Name, request.Value, request.Category ?? "Custom");
                return Ok(new { message = "Metric recorded successfully" });
            }
            catch (Exception ex)
            {
                var sanitizedMetricName = LogSanitizer.Sanitize(request.Name);
                _logger.LogError(ex, "Error recording metric {MetricName}", sanitizedMetricName);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Record execution time for an operation
        /// </summary>
        /// <param name="request">Execution time recording request</param>
        /// <returns>Success status</returns>
        [HttpPost("record-execution-time")]
        public IActionResult RecordExecutionTime([FromBody] RecordExecutionTimeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.OperationName))
                {
                    return BadRequest("Operation name is required");
                }

                var duration = TimeSpan.FromMilliseconds(request.DurationMs);
                _performanceService.RecordExecutionTime(request.OperationName, duration, request.Category ?? "Performance");
                return Ok(new { message = "Execution time recorded successfully" });
            }
            catch (Exception ex)
            {
                var sanitizedOperationName = LogSanitizer.Sanitize(request.OperationName);
                _logger.LogError(ex, "Error recording execution time for operation {OperationName}", sanitizedOperationName);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get system health based on performance metrics
        /// </summary>
        /// <returns>System health status</returns>
        [HttpGet("health")]
        public async Task<ActionResult<object>> GetHealth()
        {
            try
            {
                var snapshot = await _performanceService.GetCurrentSnapshotAsync();
                var report = await _performanceService.GeneratePerformanceReportAsync();

                var health = new
                {
                    Status = DetermineHealthStatus(snapshot, report),
                    Timestamp = DateTime.UtcNow,
                    SystemMetrics = snapshot.SystemMetrics,
                    Recommendations = report.Recommendations,
                    Issues = report.Recommendations.Any() ? "Performance issues detected" : "All systems healthy"
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance health");
                return StatusCode(500, "Internal server error");
            }
        }

        private string DetermineHealthStatus(PerformanceSnapshot snapshot, PerformanceReport report)
        {
            // Determine health status based on metrics
            var cpuUsage = snapshot.SystemMetrics.CPUUsage;
            var memoryUsage = snapshot.SystemMetrics.ProcessWorkingSet;
            var hasRecommendations = report.Recommendations.Any();

            if (cpuUsage > 90 || memoryUsage > 2000 || hasRecommendations)
            {
                return "Warning";
            }
            else if (cpuUsage > 80 || memoryUsage > 1500)
            {
                return "Caution";
            }
            else
            {
                return "Healthy";
            }
        }
    }

    public class RecordMetricRequest
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string? Category { get; set; }
    }

    public class RecordExecutionTimeRequest
    {
        public string OperationName { get; set; } = string.Empty;
        public double DurationMs { get; set; }
        public string? Category { get; set; }
    }
}
