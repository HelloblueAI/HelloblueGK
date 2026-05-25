using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using HB_NLP_Research_Lab.WebAPI.Data.Models;

namespace HB_NLP_Research_Lab.WebAPI.Services;

/// <summary>
/// Service for JWT token generation and validation
/// </summary>
public interface IJwtService
{
    string GenerateToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
    string GenerateRefreshToken();
}

public class JwtService : IJwtService
{
    private const string DefaultJwtKey = "your-super-secret-jwt-key-change-in-production-min-32-chars";

    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;
    private readonly IHostEnvironment _environment;

    public JwtService(
        IConfiguration configuration,
        ILogger<JwtService> logger,
        IHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public string GenerateToken(User user)
    {
        var jwtKey = GetJwtKey();
        
        if (jwtKey == DefaultJwtKey)
        {
            _logger.LogWarning("Using development JWT key. Configure Jwt:Key for any deployed environment.");
        }
        
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "hellobluegk";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "hellobluegk-api";
        var expirationHours = int.Parse(_configuration["Jwt:TokenExpirationHours"] ?? "24");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("userId", user.Id.ToString()),
            new Claim("username", user.Username),
        };

        if (user.IsAdmin)
        {
            claims = claims.Append(new Claim(ClaimTypes.Role, "Admin")).ToArray();
        }
        else
        {
            claims = claims.Append(new Claim(ClaimTypes.Role, "User")).ToArray();
        }

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var jwtKey = GetJwtKey();
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "hellobluegk";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "hellobluegk-api";

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return null;
        }
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private string GetJwtKey()
    {
        var jwtKey = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            if (_environment.IsDevelopment())
            {
                return DefaultJwtKey;
            }

            throw new InvalidOperationException("JWT key is not configured");
        }

        var isKnownInsecureKey = jwtKey == DefaultJwtKey || jwtKey == "change-me-in-production";
        if (!_environment.IsDevelopment() && isKnownInsecureKey)
        {
            throw new InvalidOperationException("Insecure JWT key detected outside development");
        }

        if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
        {
            throw new InvalidOperationException("JWT key must contain at least 32 bytes of entropy");
        }

        return jwtKey;
    }
}

