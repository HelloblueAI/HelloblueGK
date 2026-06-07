using System.Net;
using System.Text.Json;
using HB_NLP_Research_Lab.WebAPI.Models;

namespace HB_NLP_Research_Lab.WebAPI.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var includeExceptionDetails = _environment.IsDevelopment();
        var response = new ErrorResponse
        {
            StatusCode = context.Response.StatusCode,
            Message = exception switch
            {
                ArgumentException => includeExceptionDetails
                    ? exception.Message
                    : "The request could not be processed",
                UnauthorizedAccessException => "Unauthorized access",
                KeyNotFoundException => "Resource not found",
                InvalidOperationException => includeExceptionDetails
                    ? exception.Message
                    : "The request could not be processed",
                _ => includeExceptionDetails
                    ? exception.Message 
                    : "An error occurred while processing your request"
            },
            Details = includeExceptionDetails ? exception.ToString() : null,
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(response, options);
        await context.Response.WriteAsync(json);
    }
}


