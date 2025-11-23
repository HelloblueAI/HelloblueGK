using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents an aerospace engine configuration
/// </summary>
public class Engine
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EngineType { get; set; } = string.Empty; // Raptor, Merlin, RS-25, Custom

    public double Thrust { get; set; } // Newtons
    public double SpecificImpulse { get; set; } // seconds
    public double ChamberPressure { get; set; } // bar
    public double ExpansionRatio { get; set; }
    public double Efficiency { get; set; }
    public string Propellant { get; set; } = string.Empty;
    public double MixtureRatio { get; set; }
    public double MassFlowRate { get; set; } // kg/s

    // Metadata
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<EngineSimulation> Simulations { get; set; } = new List<EngineSimulation>();
    public ICollection<AIOptimizationRun> OptimizationRuns { get; set; } = new List<AIOptimizationRun>();
    public ICollection<DigitalTwin> DigitalTwins { get; set; } = new List<DigitalTwin>();
}

