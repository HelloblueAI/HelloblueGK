using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents a user account
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; } = false;

    /// <summary>
    /// SHA-256 hash of the currently valid refresh token (base64). Null when no refresh token is active.
    /// </summary>
    [MaxLength(128)]
    public string? RefreshTokenHash { get; set; }

    /// <summary>
    /// UTC expiry for the currently valid refresh token.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Monotonic version embedded in issued access tokens. Incremented on logout
    /// (and other full-session revocations) so previously issued JWTs fail validation.
    /// </summary>
    public int AccessTokenVersion { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User()
    {
        CreatedAt = DateTime.UtcNow;
    }

    // Navigation properties
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}

