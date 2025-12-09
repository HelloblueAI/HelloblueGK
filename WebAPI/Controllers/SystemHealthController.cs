using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HB_NLP_Research_Lab.Core;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Comprehensive system health monitoring API
    /// Provides detailed health status for all system components
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Produces("application/json")]
    [Tags("Health")]
    public class SystemHealthController : ControllerBase
    {
        private readonly ILogger<SystemHealthController> _logger;
        private readonly AdvancedHealthCheckService _healthCheckService;
        private readonly ConfigurationValidationService _configValidation;

        public SystemHealthController(
            ILogger<SystemHealthController> logger,
            AdvancedHealthCheckService healthCheckService,
            ConfigurationValidationService configValidation)
        {
            _logger = logger;
            _healthCheckService = healthCheckService;
            _configValidation = configValidation;
        }

        /// <summary>
        /// Get comprehensive system health report
        /// </summary>
        /// <returns>Detailed system health status</returns>
        /// <response code="200">System health report retrieved successfully</response>
        /// <response code="500">System health check failed</response>
        [HttpGet("comprehensive")]
        [ProducesResponseType(typeof(SystemHealthReport), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<ActionResult<SystemHealthReport>> GetComprehensiveHealth()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                
                _logger.LogInformation("Comprehensive health check completed with status: {Status}", 
                    healthReport.OverallStatus);

                return Ok(healthReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get comprehensive health report");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Health Check Failed",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Get basic system health status
        /// </summary>
        /// <returns>Basic health status</returns>
        /// <response code="200">Health status retrieved successfully</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult<object>> GetBasicStatus()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                
                var basicStatus = new
                {
                    Status = healthReport.OverallStatus.ToString(),
                    Timestamp = healthReport.Timestamp,
                    IsHealthy = healthReport.OverallStatus == AdvancedHealthStatus.Healthy,
                    ComponentCount = 5, // System, Application, Dependencies, Performance, Security
                    ErrorCount = healthReport.Errors.Count,
                    WarningCount = healthReport.SystemResources.Warnings.Count + 
                                  healthReport.ApplicationHealth.Warnings.Count +
                                  healthReport.ExternalDependencies.Warnings.Count +
                                  healthReport.PerformanceHealth.Warnings.Count +
                                  healthReport.SecurityHealth.Warnings.Count
                };

                return Ok(basicStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get basic health status");
                return Ok(new
                {
                    Status = "Unknown",
                    Timestamp = DateTime.UtcNow,
                    IsHealthy = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get system resource health
        /// </summary>
        /// <returns>System resource health status</returns>
        /// <response code="200">Resource health retrieved successfully</response>
        [HttpGet("resources")]
        [ProducesResponseType(typeof(ResourceHealth), 200)]
        public async Task<ActionResult<ResourceHealth>> GetResourceHealth()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                return Ok(healthReport.SystemResources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get resource health");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Resource Health Check Failed",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Get performance health status
        /// </summary>
        /// <returns>Performance health status</returns>
        /// <response code="200">Performance health retrieved successfully</response>
        [HttpGet("performance")]
        [ProducesResponseType(typeof(AdvancedComponentHealth), 200)]
        public async Task<ActionResult<AdvancedComponentHealth>> GetPerformanceHealth()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                return Ok(healthReport.PerformanceHealth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get performance health");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Performance Health Check Failed",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Get security health status
        /// </summary>
        /// <returns>Security health status</returns>
        /// <response code="200">Security health retrieved successfully</response>
        [HttpGet("security")]
        [ProducesResponseType(typeof(AdvancedComponentHealth), 200)]
        public async Task<ActionResult<AdvancedComponentHealth>> GetSecurityHealth()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                return Ok(healthReport.SecurityHealth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get security health");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Security Health Check Failed",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Get configuration health status
        /// </summary>
        /// <returns>Configuration health status</returns>
        /// <response code="200">Configuration health retrieved successfully</response>
        [HttpGet("configuration")]
        [ProducesResponseType(typeof(ConfigurationHealth), 200)]
        public async Task<ActionResult<ConfigurationHealth>> GetConfigurationHealth()
        {
            try
            {
                var configHealth = await _configValidation.GetConfigurationHealthAsync();
                return Ok(configHealth);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration health");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Configuration Health Check Failed",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Get health check recommendations
        /// </summary>
        /// <returns>System recommendations based on health status</returns>
        /// <response code="200">Recommendations retrieved successfully</response>
        [HttpGet("recommendations")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult<object>> GetRecommendations()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                
                var recommendations = new
                {
                    Recommendations = healthReport.Recommendations,
                    Count = healthReport.Recommendations.Count,
                    GeneratedAt = healthReport.Timestamp,
                    BasedOnStatus = healthReport.OverallStatus.ToString()
                };

                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health recommendations");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Recommendations Failed",
                    Detail = ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Get health check summary for monitoring systems
        /// </summary>
        /// <returns>Summary suitable for external monitoring</returns>
        /// <response code="200">Health summary retrieved successfully</response>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult<object>> GetHealthSummary()
        {
            try
            {
                var healthReport = await _healthCheckService.GetSystemHealthAsync();
                
                var summary = new
                {
                    Status = healthReport.OverallStatus.ToString().ToLowerInvariant(),
                    Timestamp = healthReport.Timestamp,
                    Uptime = healthReport.ApplicationHealth.Metrics.GetValueOrDefault("UptimeSeconds", 0),
                    MemoryUsageMB = healthReport.SystemResources.Metrics.GetValueOrDefault("WorkingSetMB", 0),
                    CPUUsage = healthReport.PerformanceHealth.Metrics.GetValueOrDefault("SystemCPUUsage", 0),
                    DiskSpaceGB = healthReport.ExternalDependencies.Metrics.GetValueOrDefault("DiskSpaceGB", 0),
                    ActiveMetrics = healthReport.PerformanceHealth.Metrics.GetValueOrDefault("ActiveMetrics", 0),
                    RateLimitBuckets = healthReport.SecurityHealth.Metrics.GetValueOrDefault("ActiveRateLimitBuckets", 0),
                    ErrorCount = healthReport.Errors.Count,
                    WarningCount = healthReport.SystemResources.Warnings.Count + 
                                  healthReport.ApplicationHealth.Warnings.Count +
                                  healthReport.ExternalDependencies.Warnings.Count +
                                  healthReport.PerformanceHealth.Warnings.Count +
                                  healthReport.SecurityHealth.Warnings.Count
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health summary");
                return Ok(new
                {
                    Status = "error",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message
                });
            }
        }
    }
}
