using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class TestCoverageSystemTests
{
    [Fact]
    public async Task VerifyComplianceAsync_WithEmptyRoster_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TotalFiles.Should().Be(0);
        check.StatementCoverageCompliant.Should().BeFalse();
        check.BranchCoverageCompliant.Should().BeFalse();
        check.MCDCCoverageCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("roster is empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateCoverageReportAsync_WithEmptyRoster_DoesNotMeetLevelA()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var report = await system.GenerateCoverageReportAsync();

        report.TotalFiles.Should().Be(0);
        report.MeetsDO178CLevelA.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyComplianceAsync_ClientInventedCoverageOutsideRoster_CannotForgeCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        // Perfect client-asserted coverage for a cherry-picked file, but no server roster entry.
        await system.RecordCoverageAsync("Core/Forged.cs", new CoverageMetrics
        {
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 2,
            CoveredConditions = 2,
            MCDCCoverage = 100
        });
        await system.MarkAsSafetyCriticalAsync("Core/Forged.cs", true);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("roster is empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_RosterFileWithoutEvidence_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", isSafetyCritical: true, registeredBy: "admin");

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("Missing coverage evidence", StringComparison.OrdinalIgnoreCase));
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
            CoveredStatements = 11,
            TotalBranches = 2,
            CoveredBranches = 1
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        context.CodeCoverage.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordCoverageAsync_WithZeroTotals_RejectsPercentageOnlyForge()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var act = async () => await system.RecordCoverageAsync("Core/Engine.cs", new CoverageMetrics
        {
            StatementCoverage = 100,
            BranchCoverage = 100,
            MCDCCoverage = 100,
            TotalStatements = 0,
            CoveredStatements = 0,
            TotalBranches = 0,
            CoveredBranches = 0
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        context.CodeCoverage.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordCoverageAsync_WithoutConditionTotals_ForcesMcdcToZero()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        await system.RecordCoverageAsync("Core/Engine.cs", new CoverageMetrics
        {
            MCDCCoverage = 100, // forged without condition evidence
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 0,
            CoveredConditions = 0
        });

        var coverage = await context.CodeCoverage.SingleAsync();
        coverage.MCDCCoverage.Should().Be(0.0);
        coverage.StatementCoverage.Should().Be(100.0);
        coverage.BranchCoverage.Should().Be(100.0);
    }

    [Fact]
    public async Task VerifyComplianceAsync_WithNoSafetyCriticalFiles_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: false, registeredBy: "admin");

        await system.RecordCoverageAsync("Core/Engine.cs", new CoverageMetrics
        {
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 2,
            CoveredConditions = 2,
            MCDCCoverage = 100
        });

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.MCDCCoverageCompliant.Should().BeFalse();
        check.SafetyCriticalFiles.Should().Be(0);
        check.Issues.Should().Contain(i => i.Contains("No safety-critical", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_RosterWithLevelAEvidence_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");

        await system.RecordCoverageAsync("Core/Engine.cs", new CoverageMetrics
        {
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 2,
            CoveredConditions = 2,
            MCDCCoverage = 100
        });

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
        check.MCDCCoverageCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task LinkTestCaseAsync_RejectsWhitespaceTestCaseId()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var act = async () => await system.LinkTestCaseAsync(
            "Core/Engine.cs",
            "   ",
            "Tests/EngineTests.cs",
            CoverageType.MCDC);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Test case id is required*");
    }

    [Fact]
    public async Task LinkTestCaseAsync_RejectsWhitespaceTestFile()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var act = async () => await system.LinkTestCaseAsync(
            "Core/Engine.cs",
            "TC-ENGINE-001",
            " ",
            CoverageType.MCDC);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Test file is required*");
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
