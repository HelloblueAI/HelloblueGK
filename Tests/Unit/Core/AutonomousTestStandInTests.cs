using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// Autonomous testing used to publish thrust, efficiency, reliability, and a
/// duration that were never measured. Those fields stay unset, and a missing
/// sample does not count as zero performance in the average.
/// </summary>
public class AutonomousTestStandInTests
{
    [Fact]
    public async Task DesignAndRunAutonomousTests_DoesNotInventPerformance()
    {
        var result = await new AutonomousEngineDesigner()
            .DesignAndRunAutonomousTestsAsync(new EngineArchitecture());

        result.TestScenarios.Should().NotBeEmpty();
        result.TestResults.Should().HaveCount(result.TestScenarios.Count);
        result.TestAccuracy.Should().Be(0);
        result.Analysis.PassRate.Should().Be(0);
        result.Analysis.AveragePerformance.Should().Be(0);
        result.Analysis.ReliabilityScore.Should().Be(0);
        result.TestResults.Should().OnlyContain(test =>
            test.Passed == false
            && test.TestDuration == TimeSpan.Zero
            && !test.PerformanceMetrics.ContainsKey("Thrust")
            && !test.PerformanceMetrics.ContainsKey("Efficiency")
            && !test.PerformanceMetrics.ContainsKey("Reliability"));
    }

    [Fact]
    public void AverageReportedMetric_IgnoresMissingAndNonFiniteValues()
    {
        AutonomousEngineDesigner
            .AverageReportedMetric(new List<TestResult>(), "Efficiency")
            .Should().Be(0);

        var missing = new List<TestResult>
        {
            new(),
            new() { PerformanceMetrics = null! },
            new()
            {
                PerformanceMetrics = new Dictionary<string, double>
                {
                    ["Efficiency"] = double.NaN
                }
            },
            new()
            {
                PerformanceMetrics = new Dictionary<string, double>
                {
                    ["Efficiency"] = double.PositiveInfinity
                }
            }
        };
        AutonomousEngineDesigner.AverageReportedMetric(missing, "Efficiency").Should().Be(0);

        var measured = new List<TestResult>
        {
            new()
            {
                PerformanceMetrics = new Dictionary<string, double> { ["Efficiency"] = 0 }
            },
            new()
            {
                PerformanceMetrics = new Dictionary<string, double> { ["Efficiency"] = 0.5 }
            },
            new()
        };
        AutonomousEngineDesigner
            .AverageReportedMetric(measured, "Efficiency")
            .Should().BeApproximately(0.25, 1e-9);
    }

    [Fact]
    public void PassRate_EmptyRunIsNotAPerfectScore()
    {
        AutonomousEngineDesigner.PassRate(0, 0).Should().Be(0);
        AutonomousEngineDesigner.PassRate(0, -1).Should().Be(0);
        AutonomousEngineDesigner.PassRate(1, 4).Should().BeApproximately(0.25, 1e-9);
    }
}
