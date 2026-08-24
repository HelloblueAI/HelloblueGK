using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.Core;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Data.Models;
using HB_NLP_Research_Lab.WebAPI.Models;
using HB_NLP_Research_Lab.WebAPI.Services;
using System.Security.Cryptography;
using System.Text;

namespace HB_NLP_Research_Lab.WebAPI.Controllers;

/// <summary>
/// Authentication controller for JWT token generation
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Tags("Auth")]
public class AuthController : ControllerBase
{
    private const int MaxPasswordLength = 128;
    private const int MaxPbkdf2Iterations = 600000;
    private const string DummyPasswordHash = "100000:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly HelloblueGKDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthController(
        HelloblueGKDbContext context,
        IJwtService jwtService,
        ILogger<AuthController> logger,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
        _environment = environment;
        _configuration = configuration;
    }

    /// <summary>
    /// Authenticate user and generate JWT token
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request)
    {
        try
        {
            var username = request?.Username;
            var password = request?.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
                || password.Length > MaxPasswordLength)
            {
                VerifyPassword("dummy", DummyPasswordHash);
                return InvalidCredentialsResponse();
            }

            // Case-insensitive match so normalized registrations and legacy
            // mixed-case usernames remain reachable. Prefer an ordinal exact match
            // when legacy case-variants coexist; otherwise fail closed rather than
            // issuing a token for an arbitrary FirstOrDefault row.
            var trimmedUsername = username.Trim();
            var normalizedUsername = trimmedUsername.ToLowerInvariant();
            var candidates = await _context.Users
                .Where(u => u.IsActive && u.Username.ToLower() == normalizedUsername)
                .ToListAsync();

            var user = candidates.Count switch
            {
                0 => null,
                1 => candidates[0],
                _ => candidates.FirstOrDefault(u =>
                    string.Equals(u.Username, trimmedUsername, StringComparison.Ordinal))
            };

            if (user == null)
            {
                VerifyPassword(password, DummyPasswordHash);
                if (candidates.Count > 1)
                {
                    _logger.LogWarning(
                        "Ambiguous case-variant username collision during login for: {Username}",
                        LogSanitizer.SanitizeIdentifier(username));
                }
                else
                {
                    _logger.LogWarning(
                        "Failed login attempt for username: {Username}",
                        LogSanitizer.SanitizeIdentifier(username));
                }

                return InvalidCredentialsResponse();
            }

            var isLegacyHash = IsLegacyPasswordHash(user.PasswordHash);
            if (isLegacyHash && !_environment.IsDevelopment())
            {
                VerifyPassword(password, DummyPasswordHash);
                _logger.LogWarning("Rejected legacy password hash login outside development for username: {Username}", LogSanitizer.SanitizeIdentifier(username));
                return InvalidCredentialsResponse();
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", LogSanitizer.SanitizeIdentifier(username));
                return InvalidCredentialsResponse();
            }

            // Upgrade legacy SHA256 password hash to secure PBKDF2 on successful login
            if (isLegacyHash)
            {
                _logger.LogInformation("Upgrading legacy password hash to PBKDF2 for user: {Username}", user.Username);
                user.PasswordHash = HashPassword(password);
                user.UpdatedAt = DateTime.UtcNow;
            }

            // Update last login and persist rotated refresh token in one write.
            // Bump AccessTokenVersion so previously stolen access JWTs are revoked on re-login
            // (refresh rotation alone leaves the old access token valid until expiry).
            user.LastLoginAt = DateTime.UtcNow;
            user.AccessTokenVersion += 1;
            var token = _jwtService.GenerateToken(user);
            var refreshToken = IssueRefreshToken(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {Username} logged in successfully", LogSanitizer.SanitizeIdentifier(user.Username));

            return Ok(BuildAuthResponse(user, token, refreshToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", LogSanitizer.SanitizeIdentifier(request?.Username));
            // Let the global exception handler catch this, but log it first
            throw;
        }
    }

    /// <summary>
    /// Exchange a persisted refresh token for a new access token and rotated refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return InvalidRefreshTokenResponse();
        }

        string refreshTokenHash;
        try
        {
            refreshTokenHash = _jwtService.HashRefreshToken(request.RefreshToken);
        }
        catch (ArgumentException)
        {
            return InvalidRefreshTokenResponse();
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(candidate =>
                candidate.RefreshTokenHash == refreshTokenHash && candidate.IsActive);

        if (user == null
            || user.RefreshTokenExpiresAt == null
            || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            if (user != null)
            {
                // Atomically clear only if the expired hash is still present.
                await _context.Users
                    .Where(candidate =>
                        candidate.Id == user.Id &&
                        candidate.RefreshTokenHash == refreshTokenHash)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(candidate => candidate.RefreshTokenHash, (string?)null)
                        .SetProperty(candidate => candidate.RefreshTokenExpiresAt, (DateTime?)null)
                        .SetProperty(candidate => candidate.UpdatedAt, DateTime.UtcNow));
                ClearRefreshToken(user);
            }

            return InvalidRefreshTokenResponse();
        }

        // Claim the old hash atomically so two concurrent refreshes cannot both
        // succeed with the same presented token. Mint the access JWT only after
        // reload so a concurrent logout cannot return a stale atv claim.
        var rotatedRefreshToken = _jwtService.GenerateRefreshToken();
        var rotatedRefreshTokenHash = _jwtService.HashRefreshToken(rotatedRefreshToken);
        var rotatedExpiresAt = DateTime.UtcNow.AddSeconds(_jwtService.GetRefreshTokenExpirationSeconds());
        var updatedAt = DateTime.UtcNow;

        // Rotate refresh AND bump AccessTokenVersion so any stolen access JWT
        // minted before this refresh dies immediately (not only on logout/reuse).
        var claimed = await _context.Users
            .Where(candidate =>
                candidate.Id == user.Id &&
                candidate.IsActive &&
                candidate.RefreshTokenHash == refreshTokenHash &&
                candidate.RefreshTokenExpiresAt != null &&
                candidate.RefreshTokenExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.RefreshTokenHash, rotatedRefreshTokenHash)
                .SetProperty(candidate => candidate.RefreshTokenExpiresAt, rotatedExpiresAt)
                .SetProperty(
                    candidate => candidate.AccessTokenVersion,
                    candidate => candidate.AccessTokenVersion + 1)
                .SetProperty(candidate => candidate.UpdatedAt, updatedAt));

        if (claimed == 0)
        {
            // Lost the race to another rotator, or refresh-token reuse after theft.
            // OAuth-style reuse detection: revoke refresh + bump atv so any access JWT
            // minted from the stolen refresh cannot continue to authorize.
            _logger.LogWarning(
                "Refresh token race or reuse detected for user {Username}; revoking sessions",
                LogSanitizer.SanitizeIdentifier(user.Username));
            await _context.Users
                .Where(candidate => candidate.Id == user.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.RefreshTokenHash, (string?)null)
                    .SetProperty(candidate => candidate.RefreshTokenExpiresAt, (DateTime?)null)
                    .SetProperty(
                        candidate => candidate.AccessTokenVersion,
                        candidate => candidate.AccessTokenVersion + 1)
                    .SetProperty(candidate => candidate.UpdatedAt, DateTime.UtcNow));
            return InvalidRefreshTokenResponse();
        }

        await _context.Entry(user).ReloadAsync();
        if (!user.IsActive
            || !string.Equals(user.RefreshTokenHash, rotatedRefreshTokenHash, StringComparison.Ordinal)
            || user.RefreshTokenExpiresAt == null
            || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            // Concurrent logout (or other revoke) cleared the rotated refresh after the claim.
            _logger.LogWarning(
                "Refresh discarded after concurrent revocation for user {Username}",
                LogSanitizer.SanitizeIdentifier(user.Username));
            return InvalidRefreshTokenResponse();
        }

        var accessToken = _jwtService.GenerateToken(user);

        _logger.LogInformation(
            "Refresh token rotated for user {Username}",
            LogSanitizer.SanitizeIdentifier(user.Username));

        return Ok(BuildAuthResponse(user, accessToken, rotatedRefreshToken));
    }

    /// <summary>
    /// Revoke the caller's current refresh token (JWT logout).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        await _context.Users
            .Where(candidate => candidate.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.RefreshTokenHash, (string?)null)
                .SetProperty(candidate => candidate.RefreshTokenExpiresAt, (DateTime?)null)
                .SetProperty(
                    candidate => candidate.AccessTokenVersion,
                    candidate => candidate.AccessTokenVersion + 1)
                .SetProperty(candidate => candidate.UpdatedAt, DateTime.UtcNow));

        _logger.LogInformation(
            "Access and refresh tokens revoked for user id {UserId}",
            userId);
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest? request)
    {
        // Gate on server-side configuration only — never on client-supplied flags.
        var allowPublicRegistration = _environment.IsDevelopment() ||
            _configuration.GetValue("Auth:AllowPublicRegistration", false);
        if (!allowPublicRegistration)
        {
            _logger.LogWarning(
                "Public registration attempt rejected for username: {Username}",
                LogSanitizer.SanitizeIdentifier(request?.Username));
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "Public registration is disabled",
                Timestamp = DateTime.UtcNow,
                Path = Request.Path,
                Method = Request.Method
            });
        }

        // Mirror Login: copy fields first so a request-null check is not a
        // user-controlled condition guarding token issuance (cs/user-controlled-bypass).
        var username = request?.Username;
        var email = request?.Email;
        var password = request?.Password;
        var firstName = request?.FirstName;
        var lastName = request?.LastName;

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = "Request body is required",
                Timestamp = DateTime.UtcNow,
                Path = Request.Path,
                Method = Request.Method
            });
        }

        if (password.Length > MaxPasswordLength)
        {
            return BadRequest(new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Message = $"Password cannot exceed {MaxPasswordLength} characters",
                Timestamp = DateTime.UtcNow,
                Path = Request.Path,
                Method = Request.Method
            });
        }

        // Normalize before persistence so case-variant usernames/emails cannot be
        // registered beside an existing account (closes ownership IDOR via casing).
        username = username.Trim();
        email = email.Trim();
        var normalizedUsername = username.ToLowerInvariant();
        var normalizedEmail = email.ToLowerInvariant();

        if (await _context.Users.AnyAsync(u =>
                u.Username.ToLower() == normalizedUsername ||
                u.Email.ToLower() == normalizedEmail))
        {
            // Generic message avoids username/email account enumeration.
            _logger.LogInformation(
                "Registration rejected due to existing credentials for username {Username}",
                LogSanitizer.SanitizeIdentifier(username));
            return BadRequest(new ErrorResponse
            {
                StatusCode = 400,
                Message = "Unable to register with the provided credentials",
                Timestamp = DateTime.UtcNow,
                Path = Request.Path,
                Method = Request.Method
            });
        }

        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Issue the same access + refresh pair as login so clients can refresh/logout consistently.
        var token = _jwtService.GenerateToken(user);
        var refreshToken = IssueRefreshToken(user);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Username}", LogSanitizer.SanitizeIdentifier(user.Username));

        return CreatedAtAction(nameof(Login), BuildAuthResponse(user, token, refreshToken));
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin
        });
    }

    /// <summary>
    /// Hash password using PBKDF2 with HMAC-SHA256 (secure, salted password hashing)
    /// Format: iterations:salt:hash (all base64 encoded)
    /// </summary>
    private static string HashPassword(string password)
    {
        if (password.Length > MaxPasswordLength)
        {
            throw new ArgumentException($"Password cannot exceed {MaxPasswordLength} characters", nameof(password));
        }

        // Generate a random salt for each password
        var salt = new byte[32]; // 256-bit salt
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // PBKDF2 with 100,000 iterations (adjustable based on performance requirements)
        const int iterations = 100000;
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32); // 256-bit hash

        // Format: iterations:salt:hash (all base64 encoded for storage)
        return $"{iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verify password against stored hash (supports both new PBKDF2 and legacy SHA256 for migration)
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        if (password.Length > MaxPasswordLength)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        // Check if this is a new PBKDF2 hash (format: iterations:salt:hash)
        var parts = storedHash.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[0], out var iterations))
        {
            if (iterations <= 0 || iterations > MaxPbkdf2Iterations)
            {
                return false;
            }

            try
            {
                // New PBKDF2 format
                var salt = Convert.FromBase64String(parts[1]);
                var hash = Convert.FromBase64String(parts[2]);

                if (salt.Length < 16 || hash.Length < 16 || hash.Length > 64)
                {
                    return false;
                }

                // Compute hash with the same salt and iterations
                var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password: Encoding.UTF8.GetBytes(password),
                    salt: salt,
                    iterations: iterations,
                    hashAlgorithm: HashAlgorithmName.SHA256,
                    outputLength: hash.Length);

                // Constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(hash, computedHash);
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
        else
        {
            // Legacy SHA256 format (for backward compatibility during migration)
            // In production, you should force password reset for legacy hashes
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var passwordHash = Convert.ToBase64String(hashedBytes);
            
            // Constant-time comparison
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(passwordHash),
                Encoding.UTF8.GetBytes(storedHash));
        }
    }

    private UnauthorizedObjectResult InvalidCredentialsResponse()
    {
        return Unauthorized(new ErrorResponse
        {
            StatusCode = 401,
            Message = "Invalid username or password",
            Timestamp = DateTime.UtcNow,
            Path = Request.Path,
            Method = Request.Method
        });
    }

    private UnauthorizedObjectResult InvalidRefreshTokenResponse()
    {
        return Unauthorized(new ErrorResponse
        {
            StatusCode = 401,
            Message = "Invalid or expired refresh token",
            Timestamp = DateTime.UtcNow,
            Path = Request.Path,
            Method = Request.Method
        });
    }

    private string IssueRefreshToken(User user)
    {
        var refreshToken = _jwtService.GenerateRefreshToken();
        user.RefreshTokenHash = _jwtService.HashRefreshToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(
            _jwtService.GetRefreshTokenExpirationSeconds());
        return refreshToken;
    }

    private static void ClearRefreshToken(User user)
    {
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
    }

    private LoginResponse BuildAuthResponse(User user, string accessToken, string refreshToken)
    {
        return new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwtService.GetTokenExpirationSeconds(),
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsAdmin = user.IsAdmin
            }
        };
    }

    private static bool IsLegacyPasswordHash(string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return true;
        }

        var parts = storedHash.Split(':');
        return parts.Length != 3 || !int.TryParse(parts[0], out _);
    }
}

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token exchange request
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Login response model
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public UserInfo User { get; set; } = null!;
}

/// <summary>
/// Register request model
/// </summary>
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

/// <summary>
/// Register response model (kept for compatibility; register now returns <see cref="LoginResponse"/>).
/// </summary>
public class RegisterResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public UserInfo User { get; set; } = null!;
}

/// <summary>
/// User information model
/// </summary>
public class UserInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsAdmin { get; set; }
}

