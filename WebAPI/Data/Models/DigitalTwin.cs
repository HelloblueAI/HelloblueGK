using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents a digital twin of an engine
/// </summary>
public class DigitalTwin
{
    public int Id { get; set; }
    public int EngineId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public double PredictionAccuracy { get; set; }
    public bool RealTimeLearning { get; set; } = true;
    public string? ModelDataJson { get; set; } // JSON serialized model data

    // Learning metrics
    public double? ModelImprovement { get; set; }
    public int? TrainingIterations { get; set; }
    public DateTime? LastUpdated { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Engine Engine { get; set; } = null!;
}

