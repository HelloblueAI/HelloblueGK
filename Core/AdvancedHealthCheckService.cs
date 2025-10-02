using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Advanced health check service for comprehensive system monitoring
    /// Provides detailed health status for all system components
    /// </summary>
    public class AdvancedHealthCheckService
    {
        private readonly ILogger<AdvancedHealthCheckService> _logger;
        private readonly PerformanceMonitoringService _performanceService;
        private readonly RateLimitingService _rateLimitingService;
        private readonly ConfigurationValidationService _configValidation;
        private readonly StructuredLoggingService _structuredLogging;

        public AdvancedHealthCheckService(
            ILogger<AdvancedHealthCheckService> logger,
            PerformanceMonitoringService performanceService,
            RateLimitingService rateLimitingService,
            ConfigurationValidationService configValidation,
            StructuredLoggingService structuredLogging)
        {
            _logger = logger;
            _performanceService = performanceService;
            _rateLimitingService = rateLimitingService;
            _configValidation = configValidation;
            _structuredLogging = structuredLogging;
        }

        public async Task<SystemHealthReport> GetSystemHealthAsync()
        {
            var report = new SystemHealthReport
            {
                Timestamp = DateTime.UtcNow,
                OverallStatus = HealthStatus.Healthy
            };

            try
            {
                _logger.LogInformation("Starting comprehensive system health check...");

                // Check system resources
                report.SystemResources = await CheckSystemResourcesAsync();

                // Check application health
                report.ApplicationHealth = await CheckApplicationHealthAsync();

                // Check external dependencies
                report.ExternalDependencies = await CheckExternalDependenciesAsync();

                // Check performance metrics
                report.PerformanceHealth = await CheckPerformanceHealthAsync();

                // Check security status
                report.SecurityHealth = await CheckSecurityHealthAsync();

                // Check configuration health
                report.ConfigurationHealth = await _configValidation.GetConfigurationHealthAsync();

                // Determine overall status
                report.OverallStatus = DetermineOverallStatus(report);

                // Generate recommendations
                report.Recommendations = GenerateRecommendations(report);

                _logger.LogInformation("System health check completed with status: {Status}", report.OverallStatus);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System health check failed");
                report.OverallStatus = HealthStatus.Unhealthy;
                report.Errors.Add($"Health check failed: {ex.Message}");
                return report;
            }
        }

        private async Task<ResourceHealth> CheckSystemResourcesAsync()
        {
            var resourceHealth = new ResourceHealth
            {
                Component = "System Resources",
                Status = HealthStatus.Healthy,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // Check memory usage
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64 / 1024 / 1024; // MB
                var privateMemory = process.PrivateMemorySize64 / 1024 / 1024; // MB

                resourceHealth.Metrics["WorkingSetMB"] = workingSet;
                resourceHealth.Metrics["PrivateMemoryMB"] = privateMemory;
                resourceHealth.Metrics["ThreadCount"] = process.Threads.Count;
                resourceHealth.Metrics["HandleCount"] = process.HandleCount;

                // Check GC memory
                var gcMemory = GC.GetTotalMemory(false) / 1024 / 1024; // MB
                resourceHealth.Metrics["GCMemoryMB"] = gcMemory;
                resourceHealth.Metrics["Gen0Collections"] = GC.CollectionCount(0);
                resourceHealth.Metrics["Gen1Collections"] = GC.CollectionCount(1);
                resourceHealth.Metrics["Gen2Collections"] = GC.CollectionCount(2);

                // Determine health based on thresholds
                if (workingSet > 2000) // 2GB
                {
                    resourceHealth.Status = HealthStatus.Degraded;
                    resourceHealth.Issues.Add($"High memory usage: {workingSet}MB");
                }

                if (process.Threads.Count > 500)
                {
                    resourceHealth.Status = HealthStatus.Degraded;
                    resourceHealth.Issues.Add($"High thread count: {process.Threads.Count}");
                }

                if (gcMemory > 1000) // 1GB
                {
                    resourceHealth.Warnings.Add($"High GC memory usage: {gcMemory}MB");
                }

                return resourceHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check system resources");
                resourceHealth.Status = HealthStatus.Unhealthy;
                resourceHealth.Issues.Add($"Resource check failed: {ex.Message}");
                return resourceHealth;
            }
        }

        private async Task<ComponentHealth> CheckApplicationHealthAsync()
        {
            var appHealth = new ComponentHealth
            {
                Component = "Application",
                Status = HealthStatus.Healthy,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // Check if services are initialized
                appHealth.Metrics["PerformanceMonitoringActive"] = _performanceService != null ? 1 : 0;
                appHealth.Metrics["RateLimitingActive"] = _rateLimitingService != null ? 1 : 0;
                appHealth.Metrics["StructuredLoggingActive"] = _structuredLogging != null ? 1 : 0;

                // Check uptime
                var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                appHealth.Metrics["UptimeSeconds"] = uptime.TotalSeconds;

                return appHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check application health");
                appHealth.Status = HealthStatus.Unhealthy;
                appHealth.Issues.Add($"Application check failed: {ex.Message}");
                return appHealth;
            }
        }

        private async Task<ComponentHealth> CheckExternalDependenciesAsync()
        {
            var depsHealth = new ComponentHealth
            {
                Component = "External Dependencies",
                Status = HealthStatus.Healthy,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // Check network connectivity (basic ping test)
                var networkCheck = await CheckNetworkConnectivityAsync();
                depsHealth.Metrics["NetworkConnectivity"] = networkCheck ? 1 : 0;

                if (!networkCheck)
                {
                    depsHealth.Status = HealthStatus.Degraded;
                    depsHealth.Issues.Add("Network connectivity issues detected");
                }

                // Check disk space
                var diskSpace = GetDiskSpaceInfo();
                depsHealth.Metrics["DiskSpaceGB"] = diskSpace;
                if (diskSpace < 1) // Less than 1GB
                {
                    depsHealth.Status = HealthStatus.Degraded;
                    depsHealth.Issues.Add($"Low disk space: {diskSpace}GB");
                }

                return depsHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check external dependencies");
                depsHealth.Status = HealthStatus.Unhealthy;
                depsHealth.Issues.Add($"Dependencies check failed: {ex.Message}");
                return depsHealth;
            }
        }

        private async Task<ComponentHealth> CheckPerformanceHealthAsync()
        {
            var perfHealth = new ComponentHealth
            {
                Component = "Performance",
                Status = HealthStatus.Healthy,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var snapshot = await _performanceService.GetCurrentSnapshotAsync();
                var report = await _performanceService.GeneratePerformanceReportAsync();

                perfHealth.Metrics["ActiveMetrics"] = report.ApplicationMetrics.TotalMetrics;
                perfHealth.Metrics["TotalSamples"] = report.ApplicationMetrics.TotalSamples;
                perfHealth.Metrics["SystemCPUUsage"] = snapshot.SystemMetrics.CPUUsage;
                perfHealth.Metrics["ProcessWorkingSetMB"] = snapshot.SystemMetrics.ProcessWorkingSet;

                // Check for performance issues
                if (snapshot.SystemMetrics.CPUUsage > 80)
                {
                    perfHealth.Status = HealthStatus.Degraded;
                    perfHealth.Issues.Add($"High CPU usage: {snapshot.SystemMetrics.CPUUsage:F1}%");
                }

                if (snapshot.SystemMetrics.ProcessWorkingSet > 1500)
                {
                    perfHealth.Warnings.Add($"High memory usage: {snapshot.SystemMetrics.ProcessWorkingSet}MB");
                }

                if (report.Recommendations.Count > 0)
                {
                    perfHealth.Warnings.AddRange(report.Recommendations);
                }

                return perfHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check performance health");
                perfHealth.Status = HealthStatus.Unhealthy;
                perfHealth.Issues.Add($"Performance check failed: {ex.Message}");
                return perfHealth;
            }
        }

        private async Task<ComponentHealth> CheckSecurityHealthAsync()
        {
            var secHealth = new ComponentHealth
            {
                Component = "Security",
                Status = HealthStatus.Healthy,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var rateLimitReport = await _rateLimitingService.GenerateReportAsync();
                
                secHealth.Metrics["ActiveRateLimitBuckets"] = rateLimitReport.TotalActiveBuckets;
                secHealth.Metrics["BlockedRequests"] = rateLimitReport.BlockedRequests;
                secHealth.Metrics["AllowedRequests"] = rateLimitReport.AllowedRequests;

                // Calculate block rate
                var totalRequests = rateLimitReport.BlockedRequests + rateLimitReport.AllowedRequests;
                var blockRate = totalRequests > 0 ? (double)rateLimitReport.BlockedRequests / totalRequests : 0;
                secHealth.Metrics["BlockRate"] = blockRate;

                // Check for security issues
                if (blockRate > 0.1) // More than 10% blocked
                {
                    secHealth.Warnings.Add($"High rate limit block rate: {blockRate:P1}");
                }

                if (rateLimitReport.BlockedRequests > 1000)
                {
                    secHealth.Warnings.Add("High number of blocked requests detected");
                }

                return secHealth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check security health");
                secHealth.Status = HealthStatus.Unhealthy;
                secHealth.Issues.Add($"Security check failed: {ex.Message}");
                return secHealth;
            }
        }

        private async Task<bool> CheckNetworkConnectivityAsync()
        {
            try
            {
                // Simple connectivity test - try to resolve a common DNS
                var result = await System.Net.Dns.GetHostAddressesAsync("google.com");
                return result.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private double GetDiskSpaceInfo()
        {
            try
            {
                var drive = new DriveInfo(Environment.CurrentDirectory);
                return drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0); // GB
            }
            catch
            {
                return -1; // Unknown
            }
        }

        private HealthStatus DetermineOverallStatus(SystemHealthReport report)
        {
            var statuses = new List<HealthStatus>
            {
                report.SystemResources.Status,
                report.ApplicationHealth.Status,
                report.ExternalDependencies.Status,
                report.PerformanceHealth.Status,
                report.SecurityHealth.Status
            };

            if (statuses.Any(s => s == HealthStatus.Unhealthy))
                return HealthStatus.Unhealthy;

            if (statuses.Any(s => s == HealthStatus.Degraded))
                return HealthStatus.Degraded;

            return HealthStatus.Healthy;
        }

        private List<string> GenerateRecommendations(SystemHealthReport report)
        {
            var recommendations = new List<string>();

            // System resource recommendations
            if (report.SystemResources.Metrics.ContainsKey("WorkingSetMB") && 
                (double)report.SystemResources.Metrics["WorkingSetMB"] > 1500)
            {
                recommendations.Add("Consider optimizing memory usage or increasing available memory");
            }

            // Performance recommendations
            if (report.PerformanceHealth.Metrics.ContainsKey("SystemCPUUsage") && 
                (double)report.PerformanceHealth.Metrics["SystemCPUUsage"] > 70)
            {
                recommendations.Add("High CPU usage detected, consider performance optimization");
            }

            // Security recommendations
            if (report.SecurityHealth.Metrics.ContainsKey("BlockRate") && 
                (double)report.SecurityHealth.Metrics["BlockRate"] > 0.05)
            {
                recommendations.Add("High rate limiting activity, review API usage patterns");
            }

            // Configuration recommendations
            if (report.ConfigurationHealth.WarningCount > 0)
            {
                recommendations.Add("Review configuration warnings and update settings as needed");
            }

            return recommendations;
        }
    }

    public class SystemHealthReport
    {
        public DateTime Timestamp { get; set; }
        public HealthStatus OverallStatus { get; set; }
        public ResourceHealth SystemResources { get; set; } = new();
        public ComponentHealth ApplicationHealth { get; set; } = new();
        public ComponentHealth ExternalDependencies { get; set; } = new();
        public ComponentHealth PerformanceHealth { get; set; } = new();
        public ComponentHealth SecurityHealth { get; set; } = new();
        public ConfigurationHealth ConfigurationHealth { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class ResourceHealth
    {
        public string Component { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ComponentHealth
    {
        public string Component { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
}
