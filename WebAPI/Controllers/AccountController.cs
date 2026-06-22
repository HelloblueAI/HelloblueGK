using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HB_NLP_Research_Lab.WebAPI.Controllers;

/// <summary>
/// Corporate SSO entry points for internal Swagger documentation access.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[ApiVersion("1.0")]
[Tags("Account")]
public class AccountController : ControllerBase
{
    private const string DefaultReturnUrl = "/swagger";
    private readonly IConfiguration _configuration;

    public AccountController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Start corporate SSO login for internal API documentation.
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Login([FromQuery] string? returnUrl = DefaultReturnUrl)
    {
        if (!IsOpenIdConnectEnabled())
        {
            return NotFound(new
            {
                message = "Corporate SSO is not configured. Use JWT login at /api/v1/Auth/login and paste the token into Swagger Authorize.",
                jwtLogin = "/api/v1/Auth/login"
            });
        }

        var safeReturnUrl = NormalizeReturnUrl(returnUrl);

        return Challenge(
            new AuthenticationProperties { RedirectUri = safeReturnUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Sign out of corporate SSO session for internal documentation.
    /// </summary>
    [HttpGet("logout")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult Logout()
    {
        if (!IsOpenIdConnectEnabled())
        {
            return SignOut(new AuthenticationProperties { RedirectUri = "/" },
                CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private bool IsOpenIdConnectEnabled() =>
        _configuration.GetValue("Authentication:OpenIdConnect:Enabled", false);

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return DefaultReturnUrl;
        }

        try
        {
            var decodedReturnUrl = DecodeReturnUrl(returnUrl);
            return IsSafeSwaggerReturnUrl(decodedReturnUrl) ? decodedReturnUrl : DefaultReturnUrl;
        }
        catch (UriFormatException)
        {
            return DefaultReturnUrl;
        }
    }

    private static string DecodeReturnUrl(string returnUrl)
    {
        var decodedReturnUrl = returnUrl.Trim();
        for (var i = 0; i < 3; i++)
        {
            var next = Uri.UnescapeDataString(decodedReturnUrl);
            if (string.Equals(next, decodedReturnUrl, StringComparison.Ordinal))
            {
                return decodedReturnUrl;
            }

            decodedReturnUrl = next;
        }

        return decodedReturnUrl;
    }

    private static bool IsSafeSwaggerReturnUrl(string returnUrl)
    {
        if (returnUrl.Length == 0 ||
            returnUrl.Any(char.IsControl) ||
            returnUrl.Contains('\\', StringComparison.Ordinal) ||
            returnUrl.Contains('%', StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedReturnUrl = returnUrl.StartsWith("~/", StringComparison.Ordinal)
            ? returnUrl[1..]
            : returnUrl;

        if (!normalizedReturnUrl.StartsWith('/', StringComparison.Ordinal) ||
            (normalizedReturnUrl.Length > 1 &&
             (normalizedReturnUrl[1] == '/' || normalizedReturnUrl[1] == '\\')))
        {
            return false;
        }

        var pathEnd = normalizedReturnUrl.IndexOfAny(['?', '#']);
        var path = pathEnd >= 0 ? normalizedReturnUrl[..pathEnd] : normalizedReturnUrl;

        if (path.Contains("..", StringComparison.Ordinal) ||
            path.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        const string swaggerPath = "/swagger";
        return path.Equals(swaggerPath, StringComparison.OrdinalIgnoreCase) ||
            (path.StartsWith(swaggerPath, StringComparison.OrdinalIgnoreCase) &&
             path.Length > swaggerPath.Length &&
             path[swaggerPath.Length] == '/');
    }
}
