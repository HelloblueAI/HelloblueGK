using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Rate limiting middleware for API protection
    /// Implements sliding window rate limiting with configurable policies
    /// </summary>
    public class RateLimitingMiddleware
    {
        private const int MaxAuthUsernameBodyBytes = 64 * 1024;

        // AuthController / Metrics / Certification use api/v{version:apiVersion} which substitutes "1.0".
        // Normalize /api/v1.0/... (and similar) to /api/v1/... before policy selection.
        private static readonly Regex ApiVersionPathRegex = new(
            @"^/api/v(\d+)(?:\.\d+)*(?=/|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            var endpoint = NormalizeEndpointPath(context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty);
            var clientIdentifier = ResolveClientIdentifier(context);

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
                var authBodyInspection = await InspectAuthRequestBodyAsync(context);
                if (authBodyInspection.IsPayloadTooLarge)
                {
                    await WritePayloadTooLargeResponseAsync(context);
                    return;
                }

                var usernameIdentifier = authBodyInspection.UsernameIdentifier;
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
                    catch (TimeoutException ex)
                    {
                        await HandleUsernameRateLimitingFailureAsync(context, ex, clientIdentifier, endpoint);
                        return;
                    }
                    catch (OperationCanceledException ex)
                    {
                        await HandleUsernameRateLimitingFailureAsync(context, ex, clientIdentifier, endpoint);
                        return;
                    }
                    catch (CryptographicException ex)
                    {
                        await HandleUsernameRateLimitingFailureAsync(context, ex, clientIdentifier, endpoint);
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

        internal static string ResolveClientIdentifier(HttpContext context)
        {
            // Try to get client IP address
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Do not trust forwarded IP headers unless ASP.NET Core ForwardedHeaders
            // middleware has been explicitly configured with known proxies/networks.

            // For authenticated users, use user ID instead of IP so NAT/proxy
            // clients do not share ExpensiveMutation / API quotas.
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.Identity.Name
                    ?? context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"user:{userId}";
                }
            }

            return $"ip:{clientIp}";
        }

        private bool ShouldSkipRateLimiting(string endpoint)
        {
            // Only skip anonymous probe/docs paths. Do not prefix-skip /metrics or
            // authenticated health APIs — those still pay JWT validation cost and must
            // remain capacity-protected (pre-auth IP cap + post-auth user/IP policies).
            var skipEndpoints = new[]
            {
                "/health",
                "/swagger",
                "/favicon.ico"
            };

            return skipEndpoints.Any(skip => endpoint.StartsWith(skip, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Collapse ASP.NET API-version URL segments like <c>/api/v1.0/</c> to <c>/api/v1/</c>
        /// so rate-limit policies match both version spellings.
        /// </summary>
        public static string NormalizeEndpointPath(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return endpoint;
            }

            return ApiVersionPathRegex.Replace(endpoint, "/api/v$1", 1);
        }

        private string GetPolicyNameForEndpoint(string endpoint, string method)
        {
            // API endpoint policies
            if (endpoint.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
                || endpoint.StartsWith("/api/v1/auth/register", StringComparison.OrdinalIgnoreCase)
                || endpoint.StartsWith("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
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

        private async Task HandleUsernameRateLimitingFailureAsync(
            HttpContext context,
            Exception ex,
            string clientIdentifier,
            string endpoint)
        {
            _logger.LogError(ex, "Error in username rate limiting middleware for {ClientIdentifier} on {Endpoint}",
                clientIdentifier, endpoint);

            await WriteRateLimitingUnavailableResponseAsync(context);
        }

        private static async Task WritePayloadTooLargeResponseAsync(HttpContext context)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = "Payload too large",
                message = "Authentication request payloads must be 64 KB or smaller."
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private sealed record AuthRequestBodyInspection(string? UsernameIdentifier, bool IsPayloadTooLarge);

        private static async Task<AuthRequestBodyInspection> InspectAuthRequestBodyAsync(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Body.CanRead)
            {
                return new AuthRequestBodyInspection(null, false);
            }

            if (context.Request.ContentLength is > MaxAuthUsernameBodyBytes)
            {
                return new AuthRequestBodyInspection(null, true);
            }

            try
            {
                context.Request.EnableBuffering();
                context.Request.Body.Position = 0;

                var (limitedBody, exceedsLimit) = await ReadLimitedRequestBodyAsync(
                    context.Request.Body,
                    MaxAuthUsernameBodyBytes,
                    context.RequestAborted);

                await using (limitedBody)
                {
                    context.Request.Body.Position = 0;

                    if (exceedsLimit)
                    {
                        return new AuthRequestBodyInspection(null, true);
                    }

                    var usernameIdentifier = await ExtractUsernameIdentifierAsync(
                        limitedBody,
                        context.RequestAborted);

                    context.Request.Body.Position = 0;
                    return new AuthRequestBodyInspection(usernameIdentifier, false);
                }
            }
            catch (JsonException)
            {
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }

                return new AuthRequestBodyInspection(null, false);
            }
            catch (IOException)
            {
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }

                return new AuthRequestBodyInspection(null, false);
            }
        }

        private static async Task<string?> ExtractUsernameIdentifierAsync(
            Stream body,
            CancellationToken cancellationToken)
        {
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? usernameIdentifier = null;

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
                    continue;
                }

                var normalizedUsername = username.ToUpperInvariant();
                var usernameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUsername)))[..16];
                usernameIdentifier = $"username:{usernameHash}";
            }

            return usernameIdentifier;
        }

        private static async Task<(MemoryStream LimitedBody, bool ExceedsLimit)> ReadLimitedRequestBodyAsync(
            Stream requestBody,
            int maxBytes,
            CancellationToken cancellationToken)
        {
            var limitedBody = new MemoryStream();
            var buffer = new byte[8192];
            var totalRead = 0;

            while (totalRead < maxBytes)
            {
                var bytesToRead = Math.Min(buffer.Length, maxBytes - totalRead);
                var read = await requestBody.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                limitedBody.Write(buffer, 0, read);
                totalRead += read;
            }

            var exceedsLimit = false;
            if (totalRead >= maxBytes)
            {
                var extra = new byte[1];
                var extraRead = await requestBody.ReadAsync(extra.AsMemory(), cancellationToken);
                exceedsLimit = extraRead > 0;
            }

            limitedBody.Position = 0;
            return (limitedBody, exceedsLimit);
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

        public static IApplicationBuilder UsePreAuthRateLimiting(this IApplicationBuilder app)
        {
            return app.UseMiddleware<PreAuthRateLimitingMiddleware>();
        }
    }

    /// <summary>
    /// Coarse IP cap before JWT authentication. Bounds OnTokenValidated DB lookups
    /// for Bearer and /metrics sprays without applying ExpensiveMutation (those
    /// policies key on the authenticated user after <see cref="RateLimitingMiddleware"/>).
    /// </summary>
    public sealed class PreAuthRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PreAuthRateLimitingMiddleware> _logger;
        private readonly RateLimitingService _rateLimitingService;
        private readonly RateLimitPolicy _policy;

        public PreAuthRateLimitingMiddleware(
            RequestDelegate next,
            ILogger<PreAuthRateLimitingMiddleware> logger,
            RateLimitingService rateLimitingService)
        {
            _next = next;
            _logger = logger;
            _rateLimitingService = rateLimitingService;
            _policy = new RateLimitPolicy
            {
                RequestsPerWindow = 200,
                WindowSize = TimeSpan.FromMinutes(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow
            };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!ShouldApply(context))
            {
                await _next(context);
                return;
            }

            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var identifier = $"PreAuth:ip:{clientIp}";

            RateLimitResult rateLimitResult;
            try
            {
                rateLimitResult = await _rateLimitingService.CheckRateLimitAsync(identifier, _policy);
            }
            catch (TimeoutException ex)
            {
                await WriteUnavailableAsync(ex);
                return;
            }
            catch (OperationCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
            {
                await WriteUnavailableAsync(ex);
                return;
            }
            catch (CryptographicException ex)
            {
                await WriteUnavailableAsync(ex);
                return;
            }
            catch (JsonException ex)
            {
                await WriteUnavailableAsync(ex);
                return;
            }
            catch (InvalidOperationException ex)
            {
                await WriteUnavailableAsync(ex);
                return;
            }

            async Task WriteUnavailableAsync(Exception ex)
            {
                _logger.LogError(ex, "Error in pre-auth rate limiting for {ClientIdentifier}", identifier);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"error":"Rate limiting unavailable","message":"Rate limiting is temporarily unavailable. Please try again later."}""");
            }

            if (!rateLimitResult.IsAllowed)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers["Retry-After"] = Math.Max(
                    0,
                    (int)(rateLimitResult.ResetTime - DateTime.UtcNow).TotalSeconds).ToString();
                await context.Response.WriteAsync(
                    """{"error":"Rate limit exceeded","message":"Rate limit exceeded."}""");
                return;
            }

            await _next(context);
        }

        public static bool ShouldApply(HttpContext context)
        {
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                return true;
            }

            var path = RateLimitingMiddleware.NormalizeEndpointPath(
                context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty);
            return path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
        }
    }
}
