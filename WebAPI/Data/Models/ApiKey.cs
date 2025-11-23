using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents an API key for authentication
/// </summary>
public class ApiKey
{
    public int Id { get; set; }
    public int UserId { get; set; }

    [Required]
    [MaxLength(512)]
    public string KeyHash { get; set; } = string.Empty; // Hashed API key

    [MaxLength(200)]
    public string? Name { get; set; } // User-friendly name for the key

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public User User { get; set; } = null!;
}

