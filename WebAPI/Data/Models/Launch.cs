using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents a rocket launch mission
/// </summary>
public class Launch
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string MissionName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Engine ID for the launch
    /// </summary>
    [Required]
    public int EngineId { get; set; }

    /// <summary>
    /// Number of engines (for multi-engine configurations)
    /// </summary>
    public int EngineCount { get; set; } = 1;

    /// <summary>
    /// Launch status: Scheduled, InProgress, Success, Failed, Cancelled
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "Scheduled";

    /// <summary>
    /// Launch parameters (JSON)
    /// </summary>
    public string? LaunchParametersJson { get; set; }

    /// <summary>
    /// Launch results (JSON)
    /// </summary>
    public string? ResultsJson { get; set; }

    /// <summary>
    /// Mission duration in seconds
    /// </summary>
    public double? MissionDurationSeconds { get; set; }

    /// <summary>
    /// Maximum altitude reached (meters)
    /// </summary>
    public double? MaxAltitude { get; set; }

    /// <summary>
    /// Maximum velocity reached (m/s)
    /// </summary>
    public double? MaxVelocity { get; set; }

    /// <summary>
    /// Mission success indicator
    /// </summary>
    public bool? MissionSuccess { get; set; }

    /// <summary>
    /// Error message if launch failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    // Metadata
    public DateTime ScheduledAt { get; set; }
    public DateTime? LaunchedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Engine Engine { get; set; } = null!;
}
