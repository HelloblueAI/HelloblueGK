using Microsoft.AspNetCore.Authorization;

namespace HB_NLP_Research_Lab.WebAPI.Middleware;

/// <summary>
/// Middleware to require JWT authentication for Swagger UI in production
/// Industry-standard approach: Public API docs, but interactive access requires authentication
/// Similar to how SpaceX, GitHub, Stripe handle their API documentation
/// </summary>
public class SwaggerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SwaggerAuthMiddleware> _logger;

    public SwaggerAuthMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<SwaggerAuthMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Check if this is a Swagger UI endpoint (not the JSON spec)
        // Industry practice: Allow JSON spec to be public, but protect the interactive UI
        if (path.StartsWith("/swagger/index.html", StringComparison.OrdinalIgnoreCase) ||
            path == "/swagger" ||
            (path.StartsWith("/swagger/", StringComparison.OrdinalIgnoreCase) && 
             !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            // In production, require JWT authentication (like SpaceX, GitHub do)
            if (!_environment.IsDevelopment())
            {
                // Check if user is authenticated via JWT
                if (!context.User.Identity?.IsAuthenticated ?? true)
                {
                    _logger.LogWarning("Unauthorized access attempt to Swagger UI from {RemoteIp}", 
                        context.Connection.RemoteIpAddress);
                    
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    
                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "Unauthorized",
                        message = "Swagger UI requires authentication in production.",
                        instructions = new[]
                        {
                            "1. Login at /api/v1/Auth/login to get a JWT token",
                            "2. Include 'Authorization: Bearer YOUR_TOKEN' header",
                            "3. Or use Swagger's 'Authorize' button after accessing the page"
                        },
                        loginEndpoint = "/api/v1/Auth/login",
                        note = "API documentation (JSON) is publicly accessible, but interactive UI requires authentication."
                    }));
                    return;
                }
            }
        }

        await _next(context);
    }
}

