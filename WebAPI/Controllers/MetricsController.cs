using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace HB_NLP_Research_Lab.WebAPI.Controllers;

/// <summary>
/// Prometheus metrics endpoint controller
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Tags("Metrics")]
public class MetricsController : ControllerBase
{
    private static readonly Counter ApiRequestsTotal = Metrics
        .CreateCounter("hellobluegk_api_requests_total", "Total number of API requests", new[] { "method", "endpoint", "status" });

    private static readonly Gauge AiInnovationScore = Metrics
        .CreateGauge("hellobluegk_ai_innovation_score", "AI-driven design innovation score");

    private static readonly Gauge DigitalTwinAccuracy = Metrics
        .CreateGauge("hellobluegk_digital_twin_accuracy", "Digital twin prediction accuracy");

    private static readonly Gauge QuantumAdvantage = Metrics
        .CreateGauge("hellobluegk_quantum_advantage", "Quantum-classical hybrid advantage metric");

    private static readonly Gauge EngineArchitectures = Metrics
        .CreateGauge("hellobluegk_engine_architectures", "Number of active engine architectures");

    private static readonly Gauge MultiPhysicsEfficiency = Metrics
        .CreateGauge("hellobluegk_multi_physics_efficiency", "Multi-physics coupling efficiency");

    private static readonly Counter RealTimeLearningEvents = Metrics
        .CreateCounter("hellobluegk_real_time_learning_events_total", "Total real-time learning events");

    private static readonly Histogram RequestDuration = Metrics
        .CreateHistogram("hellobluegk_request_duration_seconds", "Request duration in seconds", new[] { "method", "endpoint" });

    /// <summary>
    /// Get Prometheus metrics
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics()
    {
        // Update sample metrics (in production, these would come from actual services)
        AiInnovationScore.Set(98.5);
        DigitalTwinAccuracy.Set(99.9);
        QuantumAdvantage.Set(12.3);
        EngineArchitectures.Set(4);
        MultiPhysicsEfficiency.Set(97.0);

        using var stream = new MemoryStream();
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, CancellationToken.None);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var metrics = await reader.ReadToEndAsync();
        return Content(metrics, "text/plain");
    }

    /// <summary>
    /// Update AI innovation score metric
    /// </summary>
    [HttpPost("ai-innovation")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult UpdateAiInnovationScore([FromBody] double score)
    {
        AiInnovationScore.Set(score);
        return Ok(new { message = "AI innovation score updated", score });
    }

    /// <summary>
    /// Update digital twin accuracy metric
    /// </summary>
    [HttpPost("digital-twin-accuracy")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult UpdateDigitalTwinAccuracy([FromBody] double accuracy)
    {
        DigitalTwinAccuracy.Set(accuracy);
        return Ok(new { message = "Digital twin accuracy updated", accuracy });
    }

    /// <summary>
    /// Record a real-time learning event
    /// </summary>
    [HttpPost("learning-event")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult RecordLearningEvent()
    {
        RealTimeLearningEvents.Inc();
        return Ok(new { message = "Learning event recorded" });
    }
}

