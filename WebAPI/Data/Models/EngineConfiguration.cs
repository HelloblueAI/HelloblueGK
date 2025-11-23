namespace HB_NLP_Research_Lab.WebAPI.Data.Models;

/// <summary>
/// Represents engine configuration presets
/// </summary>
public class EngineConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty; // JSON serialized configuration
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

