namespace HB_NLP_Research_Lab.WebAPI.Models;

/// <summary>
/// Standard error response model for API errors
/// </summary>
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
}

