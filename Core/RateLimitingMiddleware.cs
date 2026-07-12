using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
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
            var policyName = GetPolicyNameForEndpoint(endpoint, context.Request.Method);
            var policy = _policies[policyName];
            var rateLimitIdentifier = $"{policyName}:{clientIdentifier}";

            RateLimitResult rateLimitResult;
            try
            {
                rateLimitResult = await _rateLimitingService.CheckRateLimitAsync(rateLimitIdentifier, policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limiting middleware for {ClientIdentifier} on {Endpoint} with {PolicyName} policy",
                    clientIdentifier, endpoint, policyName);

                await WriteRateLimitingUnavailableResponseAsync(context);
                return;
            }

            if (!rateLimitResult.IsAllowed)
            {
                await WriteRateLimitExceededResponseAsync(
                    context,
                    rateLimitResult,
                    policy,
                    clientIdentifier,
                    endpoint,
                    policyName);
                return;
            }

            if (string.Equals(policyName, "Auth", StringComparison.OrdinalIgnoreCase))
            {
                var usernameIdentifier = await GetAuthenticationUsernameIdentifierAsync(context);
                if (!string.IsNullOrWhiteSpace(usernameIdentifier))
                {
                    var usernamePolicyName = "AuthUsername";
                    var usernamePolicy = _policies[usernamePolicyName];

                    try
                    {
                        rateLimitResult = await _rateLimitingService.CheckRateLimitAsync(
                            $"{usernamePolicyName}:{usernameIdentifier}",
                            usernamePolicy);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in username rate limiting middleware for {ClientIdentifier} on {Endpoint}",
                            clientIdentifier, endpoint);

                        await WriteRateLimitingUnavailableResponseAsync(context);
                        return;
                    }

                    if (!rateLimitResult.IsAllowed)
                    {
                        await WriteRateLimitExceededResponseAsync(
                            context,
                            rateLimitResult,
                            usernamePolicy,
                            usernameIdentifier,
                            endpoint,
                            usernamePolicyName);
                        return;
                    }

                    policy = usernamePolicy;
                }
            }

            AddRateLimitHeaders(context.Response, rateLimitResult, policy);
            await _next(context);
        }

        private string GetClientIdentifier(HttpContext context)
        {
            // Try to get client IP address
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Do not trust forwarded IP headers unless ASP.NET Core ForwardedHeaders
            // middleware has been explicitly configured with known proxies/networks.

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

        private string GetPolicyNameForEndpoint(string endpoint, string method)
        {
            // API endpoint policies
            if (endpoint.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
                || endpoint.StartsWith("/api/v1/auth/register", StringComparison.OrdinalIgnoreCase)
                || endpoint.StartsWith("/api/v1/account/login", StringComparison.OrdinalIgnoreCase))
            {
                return "Auth";
            }
            else if (IsExpensiveMutationEndpoint(endpoint, method))
            {
                return "ExpensiveMutation";
            }
            else if (endpoint.StartsWith("/api/v1/ai/", StringComparison.OrdinalIgnoreCase)
                     || endpoint.StartsWith("/api/v1/aioptimization", StringComparison.OrdinalIgnoreCase))
            {
                return "AI";
            }
            else if (endpoint.StartsWith("/api/v1/performance/", StringComparison.OrdinalIgnoreCase))
            {
                return "Performance";
            }
            else if (endpoint.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            {
                return "API";
            }
            else
            {
                return "Default";
            }
        }

        private static bool IsExpensiveMutationEndpoint(string endpoint, string method)
        {
            if (!HttpMethods.IsPost(method))
            {
                return false;
            }

            return IsExactExpensiveMutationEndpoint(endpoint)
                || IsLaunchActionEndpoint(endpoint)
                || IsDigitalTwinActionEndpoint(endpoint);
        }

        private static bool IsExactExpensiveMutationEndpoint(string endpoint)
        {
            return IsEndpoint(endpoint, "/api/v1/simulations")
                || IsEndpoint(endpoint, "/api/v1/aioptimization")
                || IsEndpoint(endpoint, "/api/v1/launches")
                || IsEndpoint(endpoint, "/api/v1/digitaltwin");
        }

        private static bool IsLaunchActionEndpoint(string endpoint)
        {
            return endpoint.StartsWith("/api/v1/launches/", StringComparison.OrdinalIgnoreCase)
                && endpoint.EndsWith("/launch", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDigitalTwinActionEndpoint(string endpoint)
        {
            if (!endpoint.StartsWith("/api/v1/digitaltwin/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return endpoint.EndsWith("/learn", StringComparison.OrdinalIgnoreCase)
                || endpoint.EndsWith("/predict", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEndpoint(string endpoint, string expectedEndpoint)
        {
            return endpoint.Equals(expectedEndpoint, StringComparison.OrdinalIgnoreCase)
                || endpoint.Equals($"{expectedEndpoint}/", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task WriteRateLimitingUnavailableResponseAsync(HttpContext context)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = "Rate limiting unavailable",
                message = "Rate limiting is temporarily unavailable. Please try again later."
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private async Task WriteRateLimitExceededResponseAsync(
            HttpContext context,
            RateLimitResult rateLimitResult,
            RateLimitPolicy policy,
            string clientIdentifier,
            string endpoint,
            string policyName)
        {
            AddRateLimitHeaders(context.Response, rateLimitResult, policy);

            _logger.LogWarning("Rate limit exceeded for {ClientIdentifier} on {Endpoint} with {PolicyName} policy. Remaining: {Remaining}, Reset: {ResetTime}",
                clientIdentifier, endpoint, policyName, rateLimitResult.RemainingRequests, rateLimitResult.ResetTime);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            var retryAfter = Math.Max(0, (int)(rateLimitResult.ResetTime - DateTime.UtcNow).TotalSeconds);
            var errorResponse = new
            {
                error = "Rate limit exceeded",
                message = $"Rate limit exceeded. Try again at {rateLimitResult.ResetTime:yyyy-MM-dd HH:mm:ss UTC}",
                retryAfter,
                remainingRequests = rateLimitResult.RemainingRequests,
                resetTime = rateLimitResult.ResetTime
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private static async Task<string?> GetAuthenticationUsernameIdentifierAsync(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method)
                || !context.Request.Body.CanRead
                || context.Request.ContentLength > 64 * 1024)
            {
                return null;
            }

            try
            {
                context.Request.EnableBuffering();
                context.Request.Body.Position = 0;

                using var document = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    cancellationToken: context.RequestAborted);

                context.Request.Body.Position = 0;

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "username", StringComparison.OrdinalIgnoreCase)
                        || property.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var username = property.Value.GetString()?.Trim();
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        return null;
                    }

                    var normalizedUsername = username.ToUpperInvariant();
                    var usernameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUsername)))[..16];
                    return $"username:{usernameHash}";
                }
            }
            catch (JsonException)
            {
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }

                return null;
            }
            catch (IOException)
            {
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }

                return null;
            }

            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }

            return null;
        }

        private void AddRateLimitHeaders(HttpResponse response, RateLimitResult result, RateLimitPolicy policy)
        {
            response.Headers["X-RateLimit-Limit"] = policy.RequestsPerWindow.ToString();
            response.Headers["X-RateLimit-Remaining"] = result.RemainingRequests.ToString();
            response.Headers["X-RateLimit-Reset"] = ((DateTimeOffset)result.ResetTime).ToUnixTimeSeconds().ToString();

            if (!result.IsAllowed)
            {
                var retryAfter = Math.Max(0, (int)(result.ResetTime - DateTime.UtcNow).TotalSeconds);
                response.Headers["Retry-After"] = retryAfter.ToString();
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
                ["Auth"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 10,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                },
                ["AuthUsername"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 5,
                    WindowSize = TimeSpan.FromMinutes(15),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                },
                ["AI"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 50,
                    WindowSize = TimeSpan.FromMinutes(1),
                    Algorithm = RateLimitAlgorithm.SlidingWindow
                },
                ["ExpensiveMutation"] = new RateLimitPolicy
                {
                    RequestsPerWindow = 10,
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
