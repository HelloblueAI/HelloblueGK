using Microsoft.AspNetCore.Mvc;
using HB_NLP_Research_Lab.Core;
using System.Diagnostics;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Tags("Health")]
    public class HealthController : ControllerBase
    {
        private readonly HelloblueGKEngine _engine;

        public HealthController(HelloblueGKEngine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Basic health check endpoint
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                service = "HB-NLP Advanced Engine Design Platform",
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            });
        }

        /// <summary>
        /// Detailed health check with system metrics
        /// </summary>
        [HttpGet("detailed")]
        public IActionResult GetDetailed()
        {
            var process = Process.GetCurrentProcess();
            
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                service = "HB-NLP Advanced Engine Design Platform",
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                system = new
                {
                    uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime(),
                    memoryUsage = process.WorkingSet64,
                    cpuTime = process.TotalProcessorTime,
                    threadCount = process.Threads.Count,
                    processId = process.Id
                },
                endpoints = new
                {
                    engines = "/api/engines",
                    health = "/health",
                    swagger = "/swagger"
                }
            });
        }

        /// <summary>
        /// Engine system health check
        /// </summary>
        [HttpGet("engine")]
        public async Task<IActionResult> GetEngineHealth()
        {
            // Simulate async health check
            await Task.Delay(1);
            
            try
            {
                // Check if core engine systems are operational
                var isHealthy = true;
                var issues = new List<string>();

                // Basic system checks
                if (_engine == null)
                {
                    isHealthy = false;
                    issues.Add("Core engine not initialized");
                }

                // Memory check
                var process = Process.GetCurrentProcess();
                var memoryUsageMB = process.WorkingSet64 / (1024 * 1024);
                if (memoryUsageMB > 1000) // Warning if over 1GB
                {
                    issues.Add($"High memory usage: {memoryUsageMB:F1} MB");
                }

                return Ok(new
                {
                    status = isHealthy ? "Healthy" : "Degraded",
                    timestamp = DateTime.UtcNow,
                    engine = "HB-NLP Advanced Engine",
                    memoryUsageMB = memoryUsageMB,
                    issues = issues,
                    recommendations = issues.Any() ? new[] { "Monitor memory usage", "Check system resources" } : new string[0]
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Unhealthy",
                    timestamp = DateTime.UtcNow,
                    error = ex.Message,
                    recommendation = "Check application logs and restart if necessary"
                });
            }
        }
    }
}
