using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            // Validate request is not null
            if (request == null)
            {
                return BadRequest(new ErrorResponse
                {
                    StatusCode = 400,
                    Message = "Request body is required",
                    Timestamp = DateTime.UtcNow,
                    Path = Request.Path,
                    Method = Request.Method
                });
            }

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Unauthorized(new ErrorResponse
                {
                    StatusCode = 401,
                    Message = "Username and password are required",
                    Timestamp = DateTime.UtcNow,
                    Path = Request.Path,
                    Method = Request.Method
                });
            }

            if (request.Password.Length > MaxPasswordLength)
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

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

            if (user == null)
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                return InvalidCredentialsResponse();
            }

            var isLegacyHash = IsLegacyPasswordHash(user.PasswordHash);
            if (isLegacyHash && !_environment.IsDevelopment())
            {
                _logger.LogWarning("Rejected legacy password hash login outside development for username: {Username}", request.Username);
                return InvalidCredentialsResponse();
            }

            if (!VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                return InvalidCredentialsResponse();
            }

            // Upgrade legacy SHA256 password hash to secure PBKDF2 on successful login
            if (isLegacyHash)
            {
                _logger.LogInformation("Upgrading legacy password hash to PBKDF2 for user: {Username}", user.Username);
                user.PasswordHash = HashPassword(request.Password);
                user.UpdatedAt = DateTime.UtcNow;
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            _logger.LogInformation("User {Username} logged in successfully", user.Username);

            return Ok(new LoginResponse
            {
                Token = token,
                RefreshToken = refreshToken,
                ExpiresIn = 86400, // 24 hours in seconds
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    IsAdmin = user.IsAdmin
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", request?.Username ?? "unknown");
            // Let the global exception handler catch this, but log it first
            throw;
        }
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (request == null)
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

        if (request.Password.Length > MaxPasswordLength)
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

        var allowPublicRegistration = _environment.IsDevelopment() ||
            _configuration.GetValue("Auth:AllowPublicRegistration", false);
        if (!allowPublicRegistration)
        {
            _logger.LogWarning("Public registration attempt rejected for username: {Username}", request.Username);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse
            {
                StatusCode = StatusCodes.Status403Forbidden,
                Message = "Public registration is disabled",
                Timestamp = DateTime.UtcNow,
                Path = Request.Path,
                Method = Request.Method
            });
        }

        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new ErrorResponse
            {
                StatusCode = 400,
                Message = "Username already exists",
                Timestamp = DateTime.UtcNow
            });
        }

        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new ErrorResponse
            {
                StatusCode = 400,
                Message = "Email already exists",
                Timestamp = DateTime.UtcNow
            });
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwtService.GenerateToken(user);

        _logger.LogInformation("New user registered: {Username}", user.Username);

        return CreatedAtAction(nameof(Login), new RegisterResponse
        {
            Token = token,
            User = new UserInfo
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsAdmin = user.IsAdmin
            }
        });
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
/// Register response model
/// </summary>
public class RegisterResponse
{
    public string Token { get; set; } = string.Empty;
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

