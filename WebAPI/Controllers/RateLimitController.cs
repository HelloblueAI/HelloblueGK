using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HB_NLP_Research_Lab.Core;

namespace HB_NLP_Research_Lab.WebAPI.Controllers
{
    /// <summary>
    /// Rate limiting management API controller
    /// Provides rate limit status, configuration, and management
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Tags("Rate Limiting")]
    public class RateLimitController : ControllerBase
    {
        private readonly ILogger<RateLimitController> _logger;
        private readonly RateLimitingService _rateLimitingService;

        public RateLimitController(ILogger<RateLimitController> logger, RateLimitingService rateLimitingService)
        {
            _logger = logger;
            _rateLimitingService = rateLimitingService;
        }

        /// <summary>
        /// Get rate limit status for a specific identifier
        /// </summary>
        /// <param name="identifier">Client identifier (IP address or user ID)</param>
        /// <returns>Rate limit status</returns>
        [HttpGet("status/{identifier}")]
        public async Task<ActionResult<RateLimitStatus>> GetStatus(string identifier)
        {
            try
            {
                var status = await _rateLimitingService.GetRateLimitStatusAsync(identifier);
                return Ok(status);
            }
            catch (Exception ex)
            {
                var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(identifier);
                _logger.LogError(ex, "Error getting rate limit status for {Identifier}", sanitizedIdentifier);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get comprehensive rate limiting report
        /// </summary>
        /// <returns>Rate limiting statistics and report</returns>
        [HttpGet("report")]
        public async Task<ActionResult<RateLimitReport>> GetReport()
        {
            try
            {
                var report = await _rateLimitingService.GenerateReportAsync();
                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating rate limit report");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Reset rate limit for a specific identifier
        /// </summary>
        /// <param name="identifier">Client identifier to reset</param>
        /// <returns>Success status</returns>
        [HttpPost("reset/{identifier}")]
        public async Task<IActionResult> ResetRateLimit(string identifier)
        {
            try
            {
                await _rateLimitingService.ResetRateLimitAsync(identifier);
                var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(identifier);
                _logger.LogInformation("Rate limit reset for {Identifier}", sanitizedIdentifier);
                return Ok(new { message = $"Rate limit reset for {identifier}" });
            }
            catch (Exception ex)
            {
                var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(identifier);
                _logger.LogError(ex, "Error resetting rate limit for {Identifier}", sanitizedIdentifier);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Reset all rate limits (admin only)
        /// </summary>
        /// <returns>Success status</returns>
        [HttpPost("reset-all")]
        public async Task<IActionResult> ResetAllRateLimits()
        {
            try
            {
                await _rateLimitingService.ResetAllRateLimitsAsync();
                _logger.LogWarning("All rate limits have been reset by admin");
                return Ok(new { message = "All rate limits have been reset" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting all rate limits");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Test rate limiting for a specific identifier
        /// </summary>
        /// <param name="request">Rate limit test request</param>
        /// <returns>Rate limit test result</returns>
        [HttpPost("test")]
        public async Task<ActionResult<RateLimitResult>> TestRateLimit([FromBody] TestRateLimitRequest request)
        {
            try
            {
                var policy = new RateLimitPolicy
                {
                    RequestsPerWindow = request.MaxRequests,
                    WindowSize = TimeSpan.FromSeconds(request.WindowSeconds),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                };

                var result = await _rateLimitingService.CheckRateLimitAsync(request.Identifier, policy);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var sanitizedIdentifier = LogSanitizer.SanitizeIdentifier(request.Identifier);
                _logger.LogError(ex, "Error testing rate limit for {Identifier}", sanitizedIdentifier);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get current rate limit policies
        /// </summary>
        /// <returns>Available rate limit policies</returns>
        [HttpGet("policies")]
        public ActionResult<object> GetPolicies()
        {
            var policies = new
            {
                Default = new { RequestsPerWindow = 100, WindowSize = "1 minute", Algorithm = "SlidingWindow" },
                API = new { RequestsPerWindow = 200, WindowSize = "1 minute", Algorithm = "SlidingWindow" },
                AI = new { RequestsPerWindow = 50, WindowSize = "1 minute", Algorithm = "SlidingWindow" },
                Performance = new { RequestsPerWindow = 300, WindowSize = "1 minute", Algorithm = "SlidingWindow" }
            };

            return Ok(policies);
        }

        /// <summary>
        /// Get rate limit health status
        /// </summary>
        /// <returns>Rate limiting system health</returns>
        [HttpGet("health")]
        public async Task<ActionResult<object>> GetHealth()
        {
            try
            {
                var report = await _rateLimitingService.GenerateReportAsync();
                
                var health = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    ActiveBuckets = report.TotalActiveBuckets,
                    TotalBuckets = report.TotalBuckets,
                    BlockedRequests = report.BlockedRequests,
                    AllowedRequests = report.AllowedRequests,
                    BlockRate = report.AllowedRequests > 0 ? (double)report.BlockedRequests / (report.AllowedRequests + report.BlockedRequests) : 0
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limit health");
                return StatusCode(500, "Internal server error");
            }
        }
    }

    public class TestRateLimitRequest
    {
        public string Identifier { get; set; } = string.Empty;
        public int MaxRequests { get; set; } = 10;
        public int WindowSeconds { get; set; } = 60;
    }
}
