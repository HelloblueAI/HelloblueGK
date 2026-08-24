using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace HB_NLP_Research_Lab.WebAPI.Controllers;

/// <summary>
/// Prometheus metrics endpoint controller.
/// Trust gauges must not be client/admin-writable or seeded with sample values —
/// they share <see cref="Metrics.DefaultRegistry"/> with the scrape endpoint.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize(Roles = "Admin")]
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
    /// Export the current Prometheus registry (no sample trust-gauge seeding).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics()
    {
        // Keep gauges registered but do not invent trust values. Real producers
        // (twin/AI services) may Set them; until then scrapes report the default 0.
        _ = ApiRequestsTotal;
        _ = AiInnovationScore;
        _ = DigitalTwinAccuracy;
        _ = QuantumAdvantage;
        _ = EngineArchitectures;
        _ = MultiPhysicsEfficiency;
        _ = RequestDuration;

        using var stream = new MemoryStream();
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, CancellationToken.None);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var metrics = await reader.ReadToEndAsync();
        return Content(metrics, "text/plain");
    }

    /// <summary>
    /// Record a real-time learning event counter increment (not a trust score).
    /// </summary>
    [HttpPost("learning-event")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult RecordLearningEvent()
    {
        RealTimeLearningEvents.Inc();
        return Ok(new { message = "Learning event recorded" });
    }
}
