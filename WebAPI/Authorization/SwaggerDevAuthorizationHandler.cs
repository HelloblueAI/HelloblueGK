using HB_NLP_Research_Lab.WebAPI.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace HB_NLP_Research_Lab.WebAPI.Authorization;

/// <summary>
/// Allows public Swagger access in development when configured, while keeping production internal-only.
/// </summary>
public sealed class SwaggerDevAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly IHostEnvironment _environment;
    private readonly DocumentationOptions _documentationOptions;

    public SwaggerDevAuthorizationHandler(
        IHostEnvironment environment,
        IOptions<DocumentationOptions> documentationOptions)
    {
        _environment = environment;
        _documentationOptions = documentationOptions.Value;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Succeeded
            && _environment.IsDevelopment()
            && _documentationOptions.AllowPublicInDevelopment
            && context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
