using System.ComponentModel.DataAnnotations;

namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents an AI-driven optimization run
/// </summary>
public class AIOptimizationRun
{
    public int Id { get; set; }
    public int EngineId { get; set; }

    [MaxLength(100)]
    public string AlgorithmType { get; set; } = string.Empty; // Genetic, NeuralNetwork, ReinforcementLearning

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed

    // Optimization parameters
    public string? ParametersJson { get; set; }
    public string? ResultsJson { get; set; }

    // Performance metrics
    public double? ImprovementPercentage { get; set; }
    public int? Generations { get; set; }
    public double? BestFitness { get; set; }
    public double? ExecutionTimeSeconds { get; set; }

    // Error information
    public string? ErrorMessage { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation properties
    public Engine Engine { get; set; } = null!;
}

