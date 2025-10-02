using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Rate limiting middleware for API protection
    /// Implements sliding window rate limiting with configurable policies
    /// </summary>
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly RateLimitingService _rateLimitingService;
        private readonly Dictionary<string, RateLimitPolicy> _policies;

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, RateLimitingService rateLimitingService)
        {
            _next = next;
            _logger = logger;
            _rateLimitingService = rateLimitingService;
            _policies = InitializePolicies();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            var clientIdentifier = GetClientIdentifier(context);

            // Skip rate limiting for health checks and metrics endpoints
            if (ShouldSkipRateLimiting(endpoint))
            {
                await _next(context);
                return;
            }

            // Get rate limit policy for the endpoint
            var policy = GetPolicyForEndpoint(endpoint);

            try
            {
                var rateLimitResult = await _rateLimitingService.CheckRateLimitAsync(clientIdentifier, policy);

                // Add rate limit headers
                AddRateLimitHeaders(context.Response, rateLimitResult);

                if (!rateLimitResult.IsAllowed)
                {
                    _logger.LogWarning("Rate limit exceeded for {ClientIdentifier} on {Endpoint}. Remaining: {Remaining}, Reset: {ResetTime}", 
                        clientIdentifier, endpoint, rateLimitResult.RemainingRequests, rateLimitResult.ResetTime);

                    context.Response.StatusCode = 429; // Too Many Requests
                    context.Response.ContentType = "application/json";

                    var errorResponse = new
                    {
                        error = "Rate limit exceeded",
                        message = $"Rate limit exceeded. Try again at {rateLimitResult.ResetTime:yyyy-MM-dd HH:mm:ss UTC}",
                        retryAfter = (int)(rateLimitResult.ResetTime - DateTime.UtcNow).TotalSeconds,
                        remainingRequests = rateLimitResult.RemainingRequests,
                        resetTime = rateLimitResult.ResetTime
                    };

                    var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    await context.Response.WriteAsync(jsonResponse);
                    return;
                }

                // Continue to the next middleware
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limiting middleware for {ClientIdentifier} on {Endpoint}", 
                    clientIdentifier, endpoint);

                // If rate limiting fails, continue without rate limiting
                await _next(context);
            }
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Try to get client IP address
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Check for forwarded headers (for reverse proxies)
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var firstIp = forwardedFor.FirstOrDefault()?.Split(',')[0]?.Trim();
                if (!string.IsNullOrEmpty(firstIp))
                {
                    clientIp = firstIp;
                }
            }

            // Check for real IP header
            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                clientIp = realIp.FirstOrDefault() ?? clientIp;
            }

            // For authenticated users, use user ID instead of IP
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.Identity.Name ?? context.User.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"user:{userId}";
                }
            }

            return $"ip:{clientIp}";
        }

        private bool ShouldSkipRateLimiting(string endpoint)
        {
            var skipEndpoints = new[]
            {
                "/health",
                "/metrics",
                "/api/v1/performance/health",
                "/swagger",
                "/favicon.ico"
            };

            return skipEndpoints.Any(skip => endpoint.StartsWith(skip, StringComparison.OrdinalIgnoreCase));
        }

        private RateLimitPolicy GetPolicyForEndpoint(string endpoint)
        {
            // API endpoint policies
            if (endpoint.StartsWith("/api/v1/ai/", StringComparison.OrdinalIgnoreCase))
            {
                return _policies["AI"];
            }
            else if (endpoint.StartsWith("/api/v1/performance/", StringComparison.OrdinalIgnoreCase))
            {
                return _policies["Performance"];
            }
            else if (endpoint.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            {
                return _policies["API"];
            }
            else
            {
                return _policies["Default"];
            }
        }

        private void AddRateLimitHeaders(HttpResponse response, RateLimitResult result)
        {
            response.Headers.Add("X-RateLimit-Limit", result.TotalRequests.ToString());
            response.Headers.Add("X-RateLimit-Remaining", result.RemainingRequests.ToString());
            response.Headers.Add("X-RateLimit-Reset", ((DateTimeOffset)result.ResetTime).ToUnixTimeSeconds().ToString());

            if (!result.IsAllowed)
            {
                var retryAfter = (int)(result.ResetTime - DateTime.UtcNow).TotalSeconds;
                response.Headers.Add("Retry-After", retryAfter.ToString());
            }
        }

        private Dictionary<string, RateLimitPolicy> InitializePolicies()
        {
            return new Dictionary<string, RateLimitPolicy>
            {
                ["Default"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 100,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                },
                ["API"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 200,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                },
                ["AI"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 50,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                },
                ["Performance"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 300,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                }
            };
        }
    }

    /// <summary>
    /// Extension methods for configuring rate limiting
    /// </summary>
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddRateLimiting(this IServiceCollection services)
        {
            services.AddSingleton<RateLimitingService>();
            return services;
        }

        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RateLimitingMiddleware>();
        }
    }
}
