using System.Threading.Tasks;
using FluentAssertions;
using HB_NLP_Research_Lab.AI;
using HB_NLP_Research_Lab.Core;
using Xunit;

namespace HelloblueGK.Tests.Unit.Core;

/// <summary>
/// AdvancedPhysicsEngine used to mark every model validated at 0.95.
/// No measurement is attached, so the report and summary stay unvalidated.
/// </summary>
public class AdvancedPhysicsEngineValidationTests
{
    [Fact]
    public async Task ValidateEngineModelAsync_DoesNotPublishAFixedScore()
    {
        var engine = new AdvancedPhysicsEngine();

        var report = await engine.ValidateEngineModelAsync("Revolutionary_Engine_1");

        report.EngineModel.Should().Be("Revolutionary_Engine_1");
        report.IsValidated.Should().BeFalse();
        report.ValidationScore.Should().Be(0);
        report.CriticalIssues.Should().Be(0);
        report.Warnings.Should().Be(0);
        report.OverallAccuracy.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateEngineModelAsync_BlankName_StaysUnmeasured(string? engineModel)
    {
        var report = await new AdvancedPhysicsEngine().ValidateEngineModelAsync(engineModel!);

        report.EngineModel.Should().BeEmpty();
        report.IsValidated.Should().BeFalse();
        report.ValidationScore.Should().Be(0);
    }

    [Fact]
    public async Task GenerateValidationSummaryAsync_StaysInvalidWithoutMeasurements()
    {
        var summary = await new AdvancedPhysicsEngine().GenerateValidationSummaryAsync();

        summary.IsValid.Should().BeFalse();
        summary.ValidationScore.Should().Be(0);
        summary.CriticalIssues.Should().Be(0);
        summary.Warnings.Should().Be(0);
        summary.TotalEnginesValidated.Should().Be(0);
        summary.AverageAccuracy.Should().Be(0);
    }

    [Fact]
    public async Task RevolutionaryDesign_ReturnsTheUnmeasuredValidationReport()
    {
        var result = await new AutonomousEngineDesigner().GenerateRevolutionaryEngineAsync(new DesignRequirements());

        result.ValidationReport.EngineModel.Should().Be(result.BestEngine.Id);
        result.ValidationReport.IsValidated.Should().BeFalse();
        result.ValidationReport.ValidationScore.Should().Be(0);
        result.ValidationReport.Warnings.Should().Be(0);
    }
}
