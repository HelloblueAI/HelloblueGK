namespace HB_NLP_Research_Lab.WebAPI.Configuration;

/// <summary>
/// Controls internal-only API documentation (Swagger) access policy.
/// </summary>
public class DocumentationOptions
{
    public const string SectionName = "Documentation";

    /// <summary>
    /// When true, Swagger requires authentication in non-development environments.
    /// </summary>
    public bool InternalOnly { get; set; } = true;

    /// <summary>
    /// When true, Swagger is reachable without login during local development.
    /// </summary>
    public bool AllowPublicInDevelopment { get; set; } = true;
}
