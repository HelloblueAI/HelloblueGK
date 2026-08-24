using System.Reflection;
using HB_NLP_Research_Lab.WebAPI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace HelloblueGK.Tests.Unit.WebAPI;

public class MetricsControllerSecurityTests
{
    [Fact]
    public void MetricsController_DoesNotExposeTrustGaugeMutators()
    {
        var mutators = typeof(MetricsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method =>
                method.GetCustomAttribute<HttpPostAttribute>() != null &&
                (method.Name.Contains("Innovation", StringComparison.OrdinalIgnoreCase) ||
                 method.Name.Contains("Accuracy", StringComparison.OrdinalIgnoreCase) ||
                 method.Name.Contains("DigitalTwin", StringComparison.OrdinalIgnoreCase)))
            .Select(method => method.Name)
            .ToList();

        mutators.Should().BeEmpty(
            "Admin POST endpoints must not forge AI/twin trust gauges into the Prometheus registry");
    }

    [Fact]
    public async Task GetMetrics_DoesNotSeedInventedTrustGauges()
    {
        // Clear any prior pollution from other tests sharing DefaultRegistry.
        AiInnovationScore.Set(0);
        DigitalTwinAccuracy.Set(0);

        var controller = new MetricsController();
        var result = await controller.GetMetrics();

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Be("text/plain");
        content.Content.Should().NotBeNull();

        // Sample setters previously forced 98.5 / 99.9 on every GET.
        content.Content!.Should().NotContain("hellobluegk_ai_innovation_score 98.5");
        content.Content.Should().NotContain("hellobluegk_digital_twin_accuracy 99.9");
        content.Content.Should().NotContain("hellobluegk_multi_physics_efficiency 97");
    }

    // Mirror the gauge names registered by MetricsController so we can reset them.
    private static readonly Gauge AiInnovationScore = Metrics
        .CreateGauge("hellobluegk_ai_innovation_score", "AI-driven design innovation score");

    private static readonly Gauge DigitalTwinAccuracy = Metrics
        .CreateGauge("hellobluegk_digital_twin_accuracy", "Digital twin prediction accuracy");
}
