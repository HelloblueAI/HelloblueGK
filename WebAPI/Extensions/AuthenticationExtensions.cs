using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace HB_NLP_Research_Lab.WebAPI.Extensions;

public static class AuthenticationExtensions
{
    public const string SmartScheme = "Smart";
    public const string SmartChallengeScheme = "SmartChallenge";

    public static bool AddHelloblueGKAuthentication(
        this WebApplicationBuilder builder,
        string jwtKey,
        string jwtIssuer,
        string jwtAudience)
    {
        var oidcSection = builder.Configuration.GetSection("Authentication:OpenIdConnect");
        var oidcEnabled = oidcSection.GetValue("Enabled", false);

        var authenticationBuilder = builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SmartScheme;
                options.DefaultChallengeScheme = SmartChallengeScheme;
            })
            .AddPolicyScheme(SmartScheme, "JWT or Cookie", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader)
                        && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
                    {
                        return CookieAuthenticationDefaults.AuthenticationScheme;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddPolicyScheme(SmartChallengeScheme, "JWT or OIDC challenge", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
                        && oidcEnabled)
                    {
                        return OpenIdConnectDefaults.AuthenticationScheme;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "HelloblueGK.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/api/v1/Account/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        if (oidcEnabled)
        {
            var authority = oidcSection["Authority"]
                ?? throw new InvalidOperationException(
                    "Authentication:OpenIdConnect:Authority is required when OpenIdConnect is enabled.");
            var clientId = oidcSection["ClientId"]
                ?? throw new InvalidOperationException(
                    "Authentication:OpenIdConnect:ClientId is required when OpenIdConnect is enabled.");
            var audience = oidcSection["Audience"];
            if (string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException(
                    "Authentication:OpenIdConnect:Audience is required when OpenIdConnect is enabled.");
            }

            var configuredCallbackUrl = oidcSection["CallbackUrl"];
            if (!builder.Environment.IsDevelopment())
            {
                if (string.IsNullOrWhiteSpace(configuredCallbackUrl))
                {
                    throw new InvalidOperationException(
                        "Authentication:OpenIdConnect:CallbackUrl is required outside development when OpenIdConnect is enabled.");
                }

                if (!Uri.TryCreate(configuredCallbackUrl, UriKind.Absolute, out var callbackUri)
                    || callbackUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidOperationException(
                        "Authentication:OpenIdConnect:CallbackUrl must be an absolute HTTPS URL outside development.");
                }
            }

            authenticationBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = authority.TrimEnd('/');
                options.ClientId = clientId;
                options.ClientSecret = oidcSection["ClientSecret"];
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.CallbackPath = "/api/v1/Account/sso-callback";
                options.SignedOutCallbackPath = "/api/v1/Account/sso-signout-callback";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                var apiScope = oidcSection["ApiScope"];
                if (!string.IsNullOrWhiteSpace(apiScope))
                {
                    options.Scope.Add(apiScope);
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = audience
                };

                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        // Render terminates TLS at the edge; Azure AD only accepts https:// callbacks.
                        if (!string.IsNullOrWhiteSpace(configuredCallbackUrl))
                        {
                            context.ProtocolMessage.RedirectUri = configuredCallbackUrl;
                        }
                        else
                        {
                            var host = context.Request.Host.Host;
                            if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                                && !host.StartsWith("127.", StringComparison.Ordinal))
                            {
                                context.ProtocolMessage.RedirectUri =
                                    $"https://{context.Request.Host.Value}{options.CallbackPath}";
                            }
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("OpenIdConnect");

                        var identity = context.Principal?.FindFirstValue(ClaimTypes.Email)
                            ?? context.Principal?.FindFirstValue("preferred_username")
                            ?? context.Principal?.Identity?.Name
                            ?? "unknown";

                        logger.LogInformation("SSO user authenticated for internal documentation: {Identity}", identity);
                        return Task.CompletedTask;
                    }
                };
            });
        }

        return oidcEnabled;
    }

    public static void ConfigureSwaggerOAuth(
        this Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions swaggerOptions,
        IConfiguration configuration)
    {
        var oidcSection = configuration.GetSection("Authentication:OpenIdConnect");
        if (!oidcSection.GetValue("Enabled", false))
        {
            return;
        }

        var authority = oidcSection["Authority"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("Authentication:OpenIdConnect:Authority is not configured.");
        var clientId = oidcSection["ClientId"]
            ?? throw new InvalidOperationException("Authentication:OpenIdConnect:ClientId is not configured.");
        var apiScope = oidcSection["ApiScope"] ?? $"api://{clientId}/.default";

        swaggerOptions.AddSecurityDefinition("oauth2", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
            Description = "Corporate SSO (Azure AD / Okta) for internal API documentation access.",
            Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
            {
                AuthorizationCode = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri($"{authority}/oauth2/v2.0/authorize"),
                    TokenUrl = new Uri($"{authority}/oauth2/v2.0/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"] = "OpenID",
                        ["profile"] = "Profile",
                        [apiScope] = "HelloblueGK API"
                    }
                }
            }
        });

        swaggerOptions.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "oauth2"
                    }
                },
                new[] { "openid", "profile", apiScope }
            }
        });
    }
}
