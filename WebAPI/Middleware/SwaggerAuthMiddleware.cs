namespace HB_NLP_Research_Lab.WebAPI.Middleware;

/// <summary>
/// Industry-standard approach: Swagger UI is publicly viewable, but API calls require authentication
/// This matches how GitHub, Stripe, SpaceX, and other major APIs work:
/// - Anyone can browse and read the API documentation
/// - But you need to authenticate to actually make API calls (via "Try it out")
/// </summary>
public class SwaggerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SwaggerAuthMiddleware> _logger;

    public SwaggerAuthMiddleware(
        RequestDelegate next,
        ILogger<SwaggerAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Swagger UI and JSON spec are now publicly accessible
        // Authentication is handled at the API endpoint level, not at the Swagger UI level
        // This is the industry standard - docs are public, but API calls require auth
        
        await _next(context);
    }
}

