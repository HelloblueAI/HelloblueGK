namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents telemetry data from engine simulations
/// </summary>
public class EngineTelemetry
{
    public int Id { get; set; }
    public int SimulationId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Telemetry data
    public double? Thrust { get; set; }
    public double? ChamberPressure { get; set; }
    public double? Temperature { get; set; }
    public double? MassFlowRate { get; set; }
    public double? Efficiency { get; set; }
    public double? SpecificImpulse { get; set; }

    // Additional metrics
    public string? MetricsJson { get; set; } // JSON for additional metrics

    // Navigation properties
    public EngineSimulation Simulation { get; set; } = null!;
}

