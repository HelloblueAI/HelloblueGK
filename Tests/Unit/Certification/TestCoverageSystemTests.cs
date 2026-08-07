using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class TestCoverageSystemTests
{
    [Fact]
    public async Task VerifyComplianceAsync_WithNoCoverageData_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TotalFiles.Should().Be(0);
        check.StatementCoverageCompliant.Should().BeFalse();
        check.BranchCoverageCompliant.Should().BeFalse();
        check.MCDCCoverageCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("No coverage data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateCoverageReportAsync_WithNoCoverageData_DoesNotMeetLevelA()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var report = await system.GenerateCoverageReportAsync();

        report.TotalFiles.Should().Be(0);
        report.MeetsDO178CLevelA.Should().BeFalse();
    }

    [Fact]
    public async Task RecordCoverageAsync_RecomputesPercentagesFromCounts()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        await system.RecordCoverageAsync("Core/Engine.cs", new CoverageMetrics
        {
            StatementCoverage = 100, // forged — counts disagree
            BranchCoverage = 100,
            MCDCCoverage = 100,
            TotalStatements = 10,
            CoveredStatements = 5,
            TotalBranches = 4,
            CoveredBranches = 2
        });

        var coverage = await context.CodeCoverage.SingleAsync();
        coverage.StatementCoverage.Should().Be(50.0);
        coverage.BranchCoverage.Should().Be(50.0);
        coverage.MeetsLevelARequirements.Should().BeFalse();
    }

    [Fact]
    public async Task RecordCoverageAsync_WhenCoveredExceedsTotal_Throws()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var act = async () => await system.RecordCoverageAsync("Core/Engine.cs", new CoverageMetrics
        {
            TotalStatements = 10,
            CoveredStatements = 11
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        context.CodeCoverage.Should().BeEmpty();
    }

    private static TestCoverageDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestCoverageDbContext>()
            .UseSqlite($"Data Source=file:test-coverage-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new TestCoverageDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
