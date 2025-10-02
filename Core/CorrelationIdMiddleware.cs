using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Middleware for correlation ID tracking across requests
    /// Provides request tracing and distributed logging support
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;
        private const string CorrelationIdHeaderName = "X-Correlation-ID";
        private const string CorrelationIdActivityName = "CorrelationId";

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);
            
            // Set correlation ID in response headers
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            
            // Set correlation ID in trace context
            Activity.Current?.SetTag("correlation_id", correlationId);
            
            // Create structured logging scope
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestId"] = context.TraceIdentifier,
                ["RequestPath"] = context.Request.Path,
                ["RequestMethod"] = context.Request.Method,
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            });

            _logger.LogInformation("Request started {RequestPath} {RequestMethod}", 
                context.Request.Path, context.Request.Method);

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await _next(context);
                stopwatch.Stop();
                
                _logger.LogInformation("Request completed {RequestPath} {RequestMethod} {StatusCode} {ElapsedMs}ms", 
                    context.Request.Path, context.Request.Method, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _logger.LogError(ex, "Request failed {RequestPath} {RequestMethod} {StatusCode} {ElapsedMs}ms", 
                    context.Request.Path, context.Request.Method, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                
                throw;
            }
        }

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            // Try to get correlation ID from request headers
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationIdValues) &&
                correlationIdValues.Count > 0 && !string.IsNullOrEmpty(correlationIdValues[0]))
            {
                return correlationIdValues[0]!;
            }

            // Try to get from trace identifier
            if (!string.IsNullOrEmpty(context.TraceIdentifier))
            {
                return context.TraceIdentifier;
            }

            // Generate new correlation ID
            return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        }
    }

    /// <summary>
    /// Extension methods for correlation ID middleware
    /// </summary>
    public static class CorrelationIdExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CorrelationIdMiddleware>();
        }

        public static string GetCorrelationId(this HttpContext context)
        {
            return context.Response.Headers["X-Correlation-ID"].FirstOrDefault() ?? 
                   context.TraceIdentifier ?? 
                   "unknown";
        }

        public static IDisposable BeginCorrelationScope(this ILogger logger, string correlationId)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });
        }
    }
}
