using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents a simulation run for an engine
/// </summary>
public class EngineSimulation
{
    public int Id { get; set; }
    public int EngineId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SimulationType { get; set; } = string.Empty; // CFD, Thermal, Structural, MultiPhysics

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed

    // Simulation parameters
    public string? ParametersJson { get; set; } // JSON serialized parameters
    public string? ResultsJson { get; set; } // JSON serialized results

    // Performance metrics
    public double? ExecutionTimeSeconds { get; set; }
    public int? Iterations { get; set; }
    public double? ConvergenceRate { get; set; }
    public double? Accuracy { get; set; }

    // Error information
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CreatedBy { get; set; }

    // Navigation properties
    public Engine Engine { get; set; } = null!;
    public ICollection<EngineTelemetry> Telemetry { get; set; } = new List<EngineTelemetry>();
}

