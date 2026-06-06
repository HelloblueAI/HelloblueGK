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
    public IActionResult Login([FromQuery] string? returnUrl = "/swagger")
    {
        if (!IsOpenIdConnectEnabled())
        {
            return NotFound(new
            {
                message = "Corporate SSO is not configured. Use JWT login at /api/v1/Auth/login and paste the token into Swagger Authorize.",
                jwtLogin = "/api/v1/Auth/login"
            });
        }

        var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/swagger" : returnUrl;
        if (!safeReturnUrl.StartsWith('/') || safeReturnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            safeReturnUrl = "/swagger";
        }

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
}
