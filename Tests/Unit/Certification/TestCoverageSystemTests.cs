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
    public async Task VerifyComplianceAsync_RosterWithCountsButNoTestLinks_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");

        await system.RecordCoverageAsync("Core/Engine.cs", LevelAMetrics());

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TestEvidenceCompliant.Should().BeFalse();
        check.FilesWithTestEvidence.Should().Be(0);
        check.Issues.Should().Contain(i => i.Contains("linked test-case evidence", StringComparison.OrdinalIgnoreCase));

        var report = await system.GenerateCoverageReportAsync();
        report.MeetsDO178CLevelA.Should().BeFalse();
        report.CoverageGaps.Should().Contain(g =>
            g.GapDescription.Contains("linked test-case evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_SafetyCriticalWithoutMcdcLink_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");
        await system.RecordCoverageAsync("Core/Engine.cs", LevelAMetrics());
        await system.LinkTestCaseAsync(
            "Core/Engine.cs",
            "TC-ENGINE-001",
            "Tests/Unit/Core/EngineTests.cs",
            CoverageType.Statement);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TestEvidenceCompliant.Should().BeFalse();
        check.FilesWithTestEvidence.Should().Be(1);
        check.SafetyCriticalFilesWithMcdcTestEvidence.Should().Be(0);
        check.Issues.Should().Contain(i => i.Contains("MC/DC test-case link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LegacyEmptyOrUnsafeLinks_FailClosed()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");
        await system.RecordCoverageAsync("Core/Engine.cs", LevelAMetrics());

        var coverage = await context.CodeCoverage.SingleAsync();
        context.CoverageTestCaseLinks.Add(new CoverageTestCaseLink
        {
            Id = Guid.NewGuid(),
            CodeCoverageId = coverage.Id,
            TestCaseId = "  ",
            TestFile = "../secrets/core.c",
            CoverageType = CoverageType.MCDC,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TestEvidenceCompliant.Should().BeFalse();
        check.FilesWithTestEvidence.Should().Be(0);
    }

    [Fact]
    public async Task LinkTestCaseAsync_RejectsEmptyIdsAndUnsafeTestFiles()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var emptyId = async () => await system.LinkTestCaseAsync(
            "Core/Engine.cs", "  ", "Tests/Unit/Core/EngineTests.cs", CoverageType.MCDC);
        await emptyId.Should().ThrowAsync<ArgumentException>().WithParameterName("testCaseId");

        var traversal = async () => await system.LinkTestCaseAsync(
            "Core/Engine.cs", "TC-1", "../Tests/Unit/Core/EngineTests.cs", CoverageType.MCDC);
        await traversal.Should().ThrowAsync<ArgumentException>().WithParameterName("testFile");

        var notUnderTests = async () => await system.LinkTestCaseAsync(
            "Core/Engine.cs", "TC-1", "Core/Engine.cs", CoverageType.MCDC);
        await notUnderTests.Should().ThrowAsync<ArgumentException>().WithParameterName("testFile");

        context.CoverageTestCaseLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyComplianceAsync_RosterWithLevelAEvidence_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");

        await system.RecordCoverageAsync("Core/Engine.cs", LevelAMetrics());
        await system.LinkTestCaseAsync(
            "Core/Engine.cs",
            "TC-ENGINE-001",
            "Tests/Unit/Core/EngineTests.cs",
            CoverageType.MCDC);

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
        check.MCDCCoverageCompliant.Should().BeTrue();
        check.TestEvidenceCompliant.Should().BeTrue();
        check.FilesWithTestEvidence.Should().Be(1);
        check.SafetyCriticalFilesWithMcdcTestEvidence.Should().Be(1);

        var report = await system.GenerateCoverageReportAsync();
        report.MeetsDO178CLevelA.Should().BeTrue();
        report.CoverageGaps.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://example.test/Core/Engine.cs")]
    [InlineData("https://example.test/Core/Engine.cs")]
    [InlineData("file:///etc/passwd")]
    [InlineData("tmp/forge.cs")]
    [InlineData("phantom/coverage.cs")]
    [InlineData("C:\\Windows\\system32\\kernel.cs")]
    public async Task RegisterRequiredFileAsync_RejectsSchemeOrOutsideTreePaths(string filePath)
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var act = async () => await system.RegisterRequiredFileAsync(filePath, isSafetyCritical: true, registeredBy: "admin");

        await act.Should().ThrowAsync<ArgumentException>();
        context.RequiredCoverageFiles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://example.test/Core/Engine.cs")]
    [InlineData("tmp/forge.cs")]
    public async Task RecordCoverageAsync_RejectsSchemeOrOutsideTreePaths(string filePath)
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var act = async () => await system.RecordCoverageAsync(filePath, new CoverageMetrics
        {
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 2,
            CoveredConditions = 2,
            MCDCCoverage = 100
        });

        await act.Should().ThrowAsync<ArgumentException>();
        context.CodeCoverage.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverSchemePath_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        context.RequiredCoverageFiles.Add(new RequiredCoverageFile
        {
            Id = Guid.NewGuid(),
            FilePath = "http://example.test/Core/Engine.cs",
            IsSafetyCritical = true,
            IsActive = true,
            RegisteredBy = "admin",
            RegisteredAt = DateTime.UtcNow
        });
        context.CodeCoverage.Add(new CodeCoverage
        {
            Id = Guid.NewGuid(),
            FilePath = "http://example.test/Core/Engine.cs",
            StatementCoverage = 100,
            BranchCoverage = 100,
            MCDCCoverage = 100,
            TotalStatements = 10,
            CoveredStatements = 10,
            TotalBranches = 4,
            CoveredBranches = 4,
            TotalConditions = 2,
            CoveredConditions = 2,
            MeetsLevelARequirements = true,
            LastUpdated = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();
        var report = await system.GenerateCoverageReportAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("Unsafe coverage evidence path", StringComparison.OrdinalIgnoreCase));
        report.MeetsDO178CLevelA.Should().BeFalse();
        report.Files.Should().BeEmpty();
        report.OverallStatementCoverage.Should().Be(0);
        report.CoverageGaps.Should().Contain(g =>
            g.FilePath == "http://example.test/Core/Engine.cs"
            && g.GapDescription.Contains("Unsafe coverage evidence path", StringComparison.OrdinalIgnoreCase));

        await system.RevokeRequiredFileAsync("HTTP://example.test/Core/Engine.cs");
        context.RequiredCoverageFiles.Single().IsActive.Should().BeFalse();

        var afterRevoke = await system.VerifyComplianceAsync();
        afterRevoke.IsCompliant.Should().BeFalse();
        afterRevoke.Issues.Should().Contain(i => i.Contains("roster is empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterRequiredFileAsync_CaseVariantLeftovers_DoesNotRecaseStoredPath()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        var older = new RequiredCoverageFile
        {
            Id = Guid.NewGuid(),
            FilePath = "core/engine.cs",
            IsSafetyCritical = true,
            IsActive = true,
            RegisteredBy = "legacy",
            RegisteredAt = DateTime.UtcNow.AddDays(-2)
        };
        var exact = new RequiredCoverageFile
        {
            Id = Guid.NewGuid(),
            FilePath = "Core/Engine.cs",
            IsSafetyCritical = true,
            IsActive = true,
            RegisteredBy = "legacy",
            RegisteredAt = DateTime.UtcNow.AddDays(-1)
        };
        context.RequiredCoverageFiles.AddRange(older, exact);
        await context.SaveChangesAsync();

        var registered = await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");

        registered.FilePath.Should().Be("Core/Engine.cs");
        context.RequiredCoverageFiles.Single(f => f.Id == exact.Id).IsActive.Should().BeTrue();
        context.RequiredCoverageFiles.Single(f => f.Id == older.Id).IsActive.Should().BeFalse();
        context.RequiredCoverageFiles.Single(f => f.Id == older.Id).FilePath.Should().Be("core/engine.cs");
    }

    [Fact]
    public async Task VerifyComplianceAsync_CaseVariantRosterAndCoverage_StillMatches()
    {
        await using var context = CreateContext();
        var system = new TestCoverageSystem(context, NullLogger<TestCoverageSystem>.Instance);

        context.RequiredCoverageFiles.Add(new RequiredCoverageFile
        {
            Id = Guid.NewGuid(),
            FilePath = "core/engine.cs",
            IsSafetyCritical = true,
            IsActive = true,
            RegisteredBy = "legacy",
            RegisteredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var registered = await system.RegisterRequiredFileAsync("Core/Engine.cs", isSafetyCritical: true, registeredBy: "admin");
        registered.FilePath.Should().Be("core/engine.cs");

        await system.RecordCoverageAsync("Core/Engine.cs", LevelAMetrics());
        await system.LinkTestCaseAsync(
            "Core/Engine.cs",
            "TC-ENGINE-001",
            "Tests/Unit/Core/EngineTests.cs",
            CoverageType.MCDC);

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
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

    private static CoverageMetrics LevelAMetrics() => new()
    {
        TotalStatements = 10,
        CoveredStatements = 10,
        TotalBranches = 4,
        CoveredBranches = 4,
        TotalConditions = 2,
        CoveredConditions = 2,
        MCDCCoverage = 100
    };

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
