using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class ProblemReportingSystemTests
{
    [Fact]
    public async Task UpdateStatusAsync_RecordsPreviousStatusAndChangedBy()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Injector anomaly",
            Description = "Observed unexpected pressure oscillation during soak-back.",
            Impact = "May affect restart reliability",
            ReportedBy = "alice"
        });

        created.Status.Should().Be(ProblemReportStatus.Open);

        await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.UnderInvestigation,
            resolution: null,
            changedBy: "bob-admin");

        var change = await fixture.Reports.ProblemReportStatusChanges.SingleAsync();
        change.OldStatus.Should().Be(ProblemReportStatus.Open);
        change.NewStatus.Should().Be(ProblemReportStatus.UnderInvestigation);
        change.ChangedBy.Should().Be("bob-admin");

        var updated = await fixture.Reports.ProblemReports.SingleAsync();
        updated.Status.Should().Be(ProblemReportStatus.UnderInvestigation);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsOpenToClosedForge()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Safety-critical leak",
            Description = "Leak near turbine",
            Impact = "safety critical failure mode",
            ReportedBy = "alice"
        });

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "closed without investigation",
            changedBy: "eve");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot transition from Open to Closed*");
    }

    [Fact]
    public async Task UpdateStatusAsync_RequiresResolutionWhenClosing()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Major mixture shift",
            Description = "Observed O/F drift",
            Impact = "major performance impact",
            ReportedBy = "alice"
        });

        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "retuned", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "   ",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a non-empty resolution*");
    }

    [Fact]
    public async Task VerifyComplianceAsync_EmptyStore_FailsClosed()
    {
        await using var fixture = CreateFixture();

        var check = await fixture.System.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("No problem reports recorded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_ProperlyClosedCritical_IsCompliant()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedCoverageTestAsync("TC-SENSOR-001", "Tests/Unit/Sensors/ChamberPressureTests.cs");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToTestAsync(created.ReportNumber, "TC-SENSOR-001");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified on stand with TC-SENSOR-001",
            changedBy: "bob");

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
        check.UnresolvedCriticalProblems.Should().Be(0);
    }

    [Fact]
    public async Task VerifyComplianceAsync_ClosedWithPhantomTestLink_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        fixture.Reports.ProblemReportTestLinks.Add(new ProblemReportTestLink
        {
            Id = Guid.NewGuid(),
            ProblemReportId = created.Id,
            TestCaseId = "TC-PHANTOM-999",
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Reports.SaveChangesAsync();

        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified on stand with invented test id",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*verified implementation evidence*recorded test case*");
    }

    [Fact]
    public async Task VerifyComplianceAsync_LegacyClosedPhantomLinks_AreNotCompliant()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        created.Status = ProblemReportStatus.Closed;
        created.Resolution = "verified on stand with invented evidence";
        created.ClosedAt = DateTime.UtcNow;
        fixture.Reports.ProblemReportTestLinks.Add(new ProblemReportTestLink
        {
            Id = Guid.NewGuid(),
            ProblemReportId = created.Id,
            TestCaseId = "TC-PHANTOM-999",
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Reports.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i =>
            i.Contains("No closed critical or major problem reports", StringComparison.Ordinal) ||
            i.Contains("without substantive resolution evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateProblemReportAsync_RejectsEmptyTitleAndDescription()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var missingTitle = async () => await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "  ",
            Description = "Observed unexpected pressure oscillation",
            Impact = "major performance impact",
            ReportedBy = "alice"
        });
        await missingTitle.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Title is required*");

        var missingDescription = async () => await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Injector anomaly",
            Description = " ",
            Impact = "major performance impact",
            ReportedBy = "alice"
        });
        await missingDescription.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Description is required*");

        fixture.Reports.ProblemReports.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsVacuousResolutionText()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedCoverageTestAsync("TC-SENSOR-001", "Tests/Unit/Sensors/ChamberPressureTests.cs");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToTestAsync(created.ReportNumber, "TC-SENSOR-001");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "done",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*substantive resolution*");
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsClosedCriticalWithoutEvidenceLink()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified on stand with replacement hardware",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*verified implementation evidence*recorded test case*");
    }

    [Fact]
    public async Task LinkToRequirementAsync_RejectsEmptyRequirementId()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        var act = async () => await system.LinkToRequirementAsync(created.ReportNumber, Guid.Empty);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*non-empty GUID*");
    }

    [Fact]
    public async Task LinkToRequirementAsync_RejectsUnknownRequirementId()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        var act = async () => await system.LinkToRequirementAsync(created.ReportNumber, Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Requirement*not found*");
    }

    [Fact]
    public async Task LinkToRequirementAsync_AcceptsExistingRequirement_AndSatisfiesCompliance()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        var requirement = await fixture.SeedRequirementWithVerifiedImplementationAsync("TC-REQ-LINK-001");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToRequirementAsync(created.ReportNumber, requirement.Id);
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified against REQ chamber-pressure limit",
            changedBy: "bob");

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task LinkToRequirementAsync_ShellRequirement_DoesNotSatisfyClose()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        var shell = await fixture.SeedShellRequirementAsync();

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToRequirementAsync(created.ReportNumber, shell.Id);
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified against a Draft requirement with no implementation evidence",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*verified implementation evidence*recorded test case*");
    }

    [Fact]
    public async Task LinkToRequirementAsync_UnverifiedPlanningLinks_DoNotSatisfyClose()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        var planning = await fixture.SeedRequirementWithTestAsync("TC-PLAN-ONLY-001");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToRequirementAsync(created.ReportNumber, planning.Id);
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "linked a planning-only requirement without verified tests",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*verified implementation evidence*recorded test case*");
    }

    [Fact]
    public async Task LinkToRequirementAsync_VerifiedCodeLink_SatisfiesCompliance()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        var requirement = await fixture.SeedRequirementWithVerifiedCodeAsync();

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToRequirementAsync(created.ReportNumber, requirement.Id);
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified against implemented chamber-pressure limit",
            changedBy: "bob");

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task LinkToTestAsync_RejectsWhitespaceTestCaseId()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(created.ReportNumber, "   ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*non-empty identifier*");
    }

    [Fact]
    public async Task LinkToTestAsync_RejectsUnknownTestCaseId()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(created.ReportNumber, "TC-FAKE-001");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Test case*not found*");
    }

    [Fact]
    public async Task VerifyComplianceAsync_MinorOnlyStore_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        await system.CreateProblemReportAsync(
            new ProblemReport
            {
                Title = "Cosmetic telemetry label",
                Description = "Display rounding differs by one count.",
                Impact = "routine observation",
                ReportedBy = "alice"
            },
            explicitSeverity: ProblemSeverity.Minor);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TotalCriticalProblems.Should().Be(0);
        check.TotalMajorProblems.Should().Be(0);
        check.Issues.Should().Contain(i =>
            i.Contains("No closed critical or major problem reports", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_RejectedOnlyCritical_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Spurious trip",
            Description = "Sensor glitch during soak-back",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Rejected, changedBy: "bob");

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.UnresolvedCriticalProblems.Should().Be(0);
        check.Issues.Should().Contain(i =>
            i.Contains("No closed critical or major problem reports", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_ClosedCriticalWithOpenMinor_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedCoverageTestAsync("TC-SENSOR-001", "Tests/Unit/Sensors/ChamberPressureTests.cs");

        var critical = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });
        await system.LinkToTestAsync(critical.ReportNumber, "TC-SENSOR-001");
        await system.UpdateStatusAsync(critical.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(critical.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            critical.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified on stand with TC-SENSOR-001",
            changedBy: "bob");

        await system.CreateProblemReportAsync(
            new ProblemReport
            {
                Title = "Open minor observation",
                Description = "Log line formatting",
                Impact = "routine observation",
                ReportedBy = "alice"
            },
            explicitSeverity: ProblemSeverity.Minor);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.UnresolvedCriticalProblems.Should().Be(0);
        check.Issues.Should().Contain(i =>
            i.Contains("Unresolved minor problem reports", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_ClosedCriticalBesideRejectedCritical_IsCompliant()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedCoverageTestAsync("TC-SENSOR-001", "Tests/Unit/Sensors/ChamberPressureTests.cs");

        var closed = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });
        await system.LinkToTestAsync(closed.ReportNumber, "TC-SENSOR-001");
        await system.UpdateStatusAsync(closed.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(closed.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            closed.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified on stand with TC-SENSOR-001",
            changedBy: "bob");

        var rejected = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Spurious trip",
            Description = "Sensor glitch during soak-back",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });
        await system.UpdateStatusAsync(rejected.ReportNumber, ProblemReportStatus.Rejected, changedBy: "bob");

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeTrue();
        check.UnresolvedCriticalProblems.Should().Be(0);
        check.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyComplianceAsync_OpenMinorBesideRejectedCritical_ReportsMinorIssue()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedCoverageTestAsync("TC-SENSOR-001", "Tests/Unit/Sensors/ChamberPressureTests.cs");

        var closed = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });
        await system.LinkToTestAsync(closed.ReportNumber, "TC-SENSOR-001");
        await system.UpdateStatusAsync(closed.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(closed.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            closed.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified on stand with TC-SENSOR-001",
            changedBy: "bob");

        var rejected = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Spurious trip",
            Description = "Sensor glitch during soak-back",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });
        await system.UpdateStatusAsync(rejected.ReportNumber, ProblemReportStatus.Rejected, changedBy: "bob");

        await system.CreateProblemReportAsync(
            new ProblemReport
            {
                Title = "Open minor observation",
                Description = "Log line formatting",
                Impact = "routine observation",
                ReportedBy = "alice"
            },
            explicitSeverity: ProblemSeverity.Minor);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.UnresolvedCriticalProblems.Should().Be(0);
        check.Issues.Should().Contain(i =>
            i.Contains("Unresolved minor problem reports", StringComparison.Ordinal));
        check.Issues.Should().NotContain(i =>
            i.Contains("Critical problems must be resolved", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverClosedCriticalWithoutEvidence_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        fixture.Reports.ProblemReports.Add(new ProblemReport
        {
            Id = Guid.NewGuid(),
            ReportNumber = $"PR-{DateTime.UtcNow.Year}-9001",
            Title = "Legacy closed critical",
            Description = "Closed before evidence links were required",
            Impact = "critical safety instrumentation fault",
            Severity = ProblemSeverity.Critical,
            Status = ProblemReportStatus.Closed,
            ReportedBy = "alice",
            Resolution = "verified on stand with replacement hardware",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ClosedAt = DateTime.UtcNow.AddDays(-1)
        });
        await fixture.Reports.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i =>
            i.Contains("No closed critical or major problem reports", StringComparison.Ordinal) ||
            i.Contains("without substantive resolution evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverClosedCriticalWithShellRequirement_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        var shell = await fixture.SeedShellRequirementAsync();

        var leftover = new ProblemReport
        {
            Id = Guid.NewGuid(),
            ReportNumber = $"PR-{DateTime.UtcNow.Year}-9002",
            Title = "Legacy closed critical",
            Description = "Closed against a Draft requirement shell",
            Impact = "critical safety instrumentation fault",
            Severity = ProblemSeverity.Critical,
            Status = ProblemReportStatus.Closed,
            ReportedBy = "alice",
            Resolution = "verified against REQ chamber-pressure limit",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ClosedAt = DateTime.UtcNow.AddDays(-1)
        };
        fixture.Reports.ProblemReports.Add(leftover);
        fixture.Reports.ProblemReportRequirementLinks.Add(new ProblemReportRequirementLink
        {
            Id = Guid.NewGuid(),
            ProblemReportId = leftover.Id,
            RequirementId = shell.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await fixture.Reports.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i =>
            i.Contains("No closed critical or major problem reports", StringComparison.Ordinal) ||
            i.Contains("without substantive resolution evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LinkToTestAsync_RejectsVacuousRtmTestFile()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedRequirementWithTestAsync("TC-EMPTY-FILE", testFile: "   ");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(created.ReportNumber, "TC-EMPTY-FILE");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Test case*not found*");
    }

    [Fact]
    public async Task LinkToTestAsync_RejectsRtmOnlyBootstrap()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedRequirementWithTestAsync("TC-RTM-ONLY", "Tests/Unit/Sensors/ForgeTests.cs");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(created.ReportNumber, "TC-RTM-ONLY");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Test case*not found*");
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverClosedCriticalWithRtmOnlyTest_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedRequirementWithTestAsync("TC-RTM-ONLY", "Tests/Unit/Sensors/ForgeTests.cs");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        created.Status = ProblemReportStatus.Closed;
        created.Resolution = "verified on stand with rtm-only planning link";
        created.ClosedAt = DateTime.UtcNow;
        fixture.Reports.ProblemReportTestLinks.Add(new ProblemReportTestLink
        {
            Id = Guid.NewGuid(),
            ProblemReportId = created.Id,
            TestCaseId = "TC-RTM-ONLY",
            CreatedAt = DateTime.UtcNow
        });
        await fixture.Reports.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i =>
            i.Contains("No closed critical or major problem reports", StringComparison.Ordinal) ||
            i.Contains("without substantive resolution evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LinkToTestAsync_AcceptsCoverageInventoryTestCase()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;
        await fixture.SeedCoverageTestAsync("TC-COV-001", "Tests/Coverage/SensorTests.cs");

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.LinkToTestAsync(created.ReportNumber, "TC-COV-001");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "verified against coverage inventory TC-COV-001",
            changedBy: "bob");

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateStatusAsync_ConcurrentOpenTransitions_PreserveAuditChain()
    {
        var sharedName = $"pr-toc-{Guid.NewGuid():N}";
        await using var fixture = CreateFixture(sharedName);
        var created = await fixture.System.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Injector anomaly",
            Description = "Observed unexpected pressure oscillation during soak-back.",
            Impact = "May affect restart reliability",
            ReportedBy = "alice"
        });

        await using var racingReports = CreateReportContext(sharedName);
        await using var racingRequirements = CreateRequirementsContext($"req-toc-{Guid.NewGuid():N}");
        var racingSystem = new ProblemReportingSystem(
            racingReports,
            NullLogger<ProblemReportingSystem>.Instance,
            racingRequirements);

        // Both callers start from Open. The atomic status claim must not produce two
        // audit rows that both claim OldStatus=Open (the pre-ExecuteUpdate last-writer-wins bug).
        var first = fixture.System.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.UnderInvestigation,
            changedBy: "alice");
        var second = racingSystem.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Rejected,
            changedBy: "eve");

        var results = await Task.WhenAll(RecordAsync(first), RecordAsync(second));
        results.Count(r => r == null).Should().BeGreaterThan(0);

        await using var audit = CreateReportContext(sharedName);
        var changes = await audit.ProblemReportStatusChanges
            .AsNoTracking()
            .OrderBy(c => c.ChangedAt)
            .ThenBy(c => c.NewStatus)
            .ToListAsync();

        changes.Should().NotBeEmpty();
        changes[0].OldStatus.Should().Be(ProblemReportStatus.Open);
        for (var i = 1; i < changes.Count; i++)
        {
            changes[i].OldStatus.Should().Be(changes[i - 1].NewStatus);
        }

        var persisted = await audit.ProblemReports.AsNoTracking().SingleAsync();
        persisted.Status.Should().Be(changes[^1].NewStatus);
    }

    private static async Task<Exception?> RecordAsync(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return ex;
        }
        catch (DbUpdateException ex)
        {
            return ex;
        }
    }

    [Fact]
    public async Task CreateProblemReportAsync_UnclassifiedImpact_DefaultsToCritical()
    {
        await using var fixture = CreateFixture();

        var created = await fixture.System.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Vague anomaly",
            Description = "Something odd on the stand",
            Impact = "Observed unexpected telemetry drift during soak-back",
            ReportedBy = "alice"
        });

        created.Severity.Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public async Task CreateProblemReportAsync_KeywordFloor_BlocksUnderClassification()
    {
        await using var fixture = CreateFixture();

        var created = await fixture.System.CreateProblemReportAsync(
            new ProblemReport
            {
                Title = "Safety leak",
                Description = "Leak near turbine",
                Impact = "safety-critical failure mode",
                ReportedBy = "alice"
            },
            explicitSeverity: ProblemSeverity.Minor);

        created.Severity.Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public async Task CreateProblemReportAsync_AllocatesMonotonicReportNumbers()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var first = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "PR A",
            Description = "a",
            Impact = "major impact",
            ReportedBy = "alice"
        });
        var second = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "PR B",
            Description = "b",
            Impact = "major impact",
            ReportedBy = "alice"
        });

        first.ReportNumber.Should().EndWith("-0001");
        second.ReportNumber.Should().EndWith("-0002");
        first.Severity.Should().Be(ProblemSeverity.Major);
    }

    [Theory]
    [InlineData(null, null, ProblemSeverity.Critical)]
    [InlineData("routine observation", null, ProblemSeverity.Minor)]
    [InlineData("major performance impact", null, ProblemSeverity.Major)]
    [InlineData("critical safety fault", ProblemSeverity.Minor, ProblemSeverity.Critical)]
    [InlineData("routine observation", ProblemSeverity.Minor, ProblemSeverity.Minor)]
    [InlineData("blocks certification gate", ProblemSeverity.Minor, ProblemSeverity.Critical)]
    [InlineData("unclassified blocking issue", ProblemSeverity.Major, ProblemSeverity.Critical)]
    [InlineData("nitrogen tank pressure anomaly", null, ProblemSeverity.Critical)]
    [InlineData("reviewer nit only", null, ProblemSeverity.Minor)]
    [InlineData("routine observation of catastrophic engine failure", null, ProblemSeverity.Critical)]
    [InlineData("routine observation of loss of vehicle", ProblemSeverity.Minor, ProblemSeverity.Critical)]
    [InlineData("minor cosmetic hazard in flight software", null, ProblemSeverity.Critical)]
    [InlineData("non-critical routine observation", null, ProblemSeverity.Minor)]
    [InlineData("non critical routine observation", null, ProblemSeverity.Minor)]
    [InlineData("cannon critical abort path", null, ProblemSeverity.Critical)]
    [InlineData("routine observation of a phenomenon hazardous to the vehicle", null, ProblemSeverity.Critical)]
    [InlineData("engine failure during max-Q ascent", null, ProblemSeverity.Critical)]
    [InlineData("routine observation of hazardous ascent condition", null, ProblemSeverity.Critical)]
    [InlineData("routine observation of critically unsafe abort path", ProblemSeverity.Minor, ProblemSeverity.Critical)]
    [InlineData("stand notes after the engine failed catastrophically", null, ProblemSeverity.Critical)]
    public void ResolveSeverity_FailClosedAndKeywordFloor(
        string? impact,
        ProblemSeverity? explicitSeverity,
        ProblemSeverity expected)
    {
        ProblemReportingSystem.ResolveSeverity(impact, explicitSeverity).Should().Be(expected);
    }

    [Fact]
    public void ResolveSeverity_TitleCatastrophicLanguage_ElevatesRoutineImpact()
    {
        ProblemReportingSystem.ResolveSeverity(
                title: "Catastrophic engine failure during ascent",
                description: "Loss of vehicle after max-Q",
                impact: "routine observation",
                explicitSeverity: ProblemSeverity.Minor)
            .Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public void ResolveSeverity_WordEndingInNon_DoesNotNegateFollowingElevationToken()
    {
        ProblemReportingSystem.ResolveSeverity(
                title: "Cannon critical abort",
                description: "Stand notes",
                impact: "routine observation",
                explicitSeverity: ProblemSeverity.Minor)
            .Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public void ResolveSeverity_RoutineTitle_DoesNotDowngradeUnclassifiedImpact()
    {
        ProblemReportingSystem.ResolveSeverity(
                title: "Routine stand checkout",
                description: "Observed unexpected telemetry drift during soak-back",
                impact: "Observed unexpected telemetry drift during soak-back",
                explicitSeverity: null)
            .Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public async Task CreateProblemReportAsync_RoutineObservationOfCatastrophicFailure_IsCritical()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(
            new ProblemReport
            {
                Title = "Ascent anomaly",
                Description = "Engine behavior during max-Q",
                Impact = "routine observation of catastrophic engine failure during max-Q ascent",
                ReportedBy = "alice"
            },
            explicitSeverity: ProblemSeverity.Minor);

        created.Severity.Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public async Task CreateProblemReportAsync_CatastrophicTitleWithRoutineImpact_IsCritical()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Catastrophic engine failure",
            Description = "Loss of thrust after MECO",
            Impact = "routine observation",
            ReportedBy = "alice"
        });

        created.Severity.Should().Be(ProblemSeverity.Critical);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsClosedCatastrophicReportWithoutEvidence()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Ascent anomaly",
            Description = "Engine behavior during max-Q",
            Impact = "routine observation of catastrophic engine failure during max-Q ascent",
            ReportedBy = "alice"
        });

        created.Severity.Should().Be(ProblemSeverity.Critical);
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "inspected hardware", changedBy: "bob");

        var act = async () => await system.UpdateStatusAsync(
            created.ReportNumber,
            ProblemReportStatus.Closed,
            resolution: "closed after stand inspection notes were recorded",
            changedBy: "bob");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*linked requirement or test case*");
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverMinorWithCatastrophicLanguage_FailsClosed()
    {
        await using var fixture = CreateFixture();
        var system = fixture.System;

        fixture.Reports.ProblemReports.Add(new ProblemReport
        {
            Id = Guid.NewGuid(),
            ReportNumber = "PR-2026-0099",
            Title = "Ascent anomaly",
            Description = "Engine behavior during max-Q",
            Impact = "routine observation of catastrophic engine failure during max-Q ascent",
            Severity = ProblemSeverity.Minor,
            Status = ProblemReportStatus.Closed,
            Resolution = "closed after stand inspection notes were recorded",
            ReportedBy = "legacy",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ClosedAt = DateTime.UtcNow.AddDays(-1)
        });
        await fixture.Reports.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TotalCriticalProblems.Should().Be(1);
        check.Issues.Should().Contain(i => i.Contains("without substantive resolution evidence", StringComparison.Ordinal));
        check.Issues.Should().Contain(i => i.Contains("elevated safety language", StringComparison.Ordinal));
    }

    private static ProblemReportFixture CreateFixture(string? sharedReportsName = null)
    {
        var reports = CreateReportContext(sharedReportsName ?? $"problem-reports-{Guid.NewGuid():N}");
        var requirements = CreateRequirementsContext($"requirements-{Guid.NewGuid():N}");
        var coverage = CreateCoverageContext($"coverage-{Guid.NewGuid():N}");
        var system = new ProblemReportingSystem(
            reports,
            NullLogger<ProblemReportingSystem>.Instance,
            requirements,
            coverage);
        return new ProblemReportFixture(reports, requirements, coverage, system);
    }

    private static ProblemReportDbContext CreateReportContext(string name)
    {
        var options = new DbContextOptionsBuilder<ProblemReportDbContext>()
            .UseSqlite($"Data Source=file:{name}?mode=memory&cache=shared")
            .Options;

        var context = new ProblemReportDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static RequirementsDbContext CreateRequirementsContext(string name)
    {
        var options = new DbContextOptionsBuilder<RequirementsDbContext>()
            .UseSqlite($"Data Source=file:{name}?mode=memory&cache=shared")
            .Options;

        var context = new RequirementsDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static TestCoverageDbContext CreateCoverageContext(string name)
    {
        var options = new DbContextOptionsBuilder<TestCoverageDbContext>()
            .UseSqlite($"Data Source=file:{name}?mode=memory&cache=shared")
            .Options;

        var context = new TestCoverageDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class ProblemReportFixture : IAsyncDisposable
    {
        public ProblemReportFixture(
            ProblemReportDbContext reports,
            RequirementsDbContext requirements,
            TestCoverageDbContext coverage,
            ProblemReportingSystem system)
        {
            Reports = reports;
            Requirements = requirements;
            Coverage = coverage;
            System = system;
        }

        public ProblemReportDbContext Reports { get; }
        public RequirementsDbContext Requirements { get; }
        public TestCoverageDbContext Coverage { get; }
        public ProblemReportingSystem System { get; }

        public async Task<Requirement> SeedRequirementWithTestAsync(
            string testCaseId,
            string testFile = "Tests/Unit/Sensors/ChamberPressureTests.cs")
        {
            var requirement = new Requirement
            {
                Id = Guid.NewGuid(),
                RequirementNumber = $"REQ-{Guid.NewGuid():N}"[..16],
                Title = "Chamber pressure instrumentation",
                Description = "Sensor must remain within calibrated range during hot-fire.",
                Priority = RequirementPriority.Critical,
                Status = RequirementStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "alice"
            };
            Requirements.Requirements.Add(requirement);
            Requirements.RequirementTestLinks.Add(new RequirementTestLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirement.Id,
                TestCaseId = testCaseId,
                TestFile = testFile,
                CoverageType = TestCoverageType.MCDC,
                CreatedAt = DateTime.UtcNow
            });
            await Requirements.SaveChangesAsync();
            return requirement;
        }

        public async Task<Requirement> SeedShellRequirementAsync()
        {
            var requirement = new Requirement
            {
                Id = Guid.NewGuid(),
                RequirementNumber = $"REQ-{Guid.NewGuid():N}"[..16],
                Title = "Untraced chamber pressure placeholder",
                Description = "Draft shell with no design, code, or test links.",
                Priority = RequirementPriority.Critical,
                Status = RequirementStatus.Draft,
                TraceabilityStatus = TraceabilityStatus.NotTraced,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "alice"
            };
            Requirements.Requirements.Add(requirement);
            await Requirements.SaveChangesAsync();
            return requirement;
        }

        public async Task<Requirement> SeedRequirementWithVerifiedImplementationAsync(string testCaseId)
        {
            var requirement = new Requirement
            {
                Id = Guid.NewGuid(),
                RequirementNumber = $"REQ-{Guid.NewGuid():N}"[..16],
                Title = "Chamber pressure instrumentation",
                Description = "Sensor must remain within calibrated range during hot-fire.",
                Priority = RequirementPriority.Critical,
                Status = RequirementStatus.Verified,
                TraceabilityStatus = TraceabilityStatus.FullyTraced,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "alice"
            };
            Requirements.Requirements.Add(requirement);
            Requirements.RequirementTestLinks.Add(new RequirementTestLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirement.Id,
                TestCaseId = testCaseId,
                TestFile = "Tests/Unit/Sensors/ChamberPressureTests.cs",
                CoverageType = TestCoverageType.MCDC,
                TestResult = TestResult.Passed,
                Verified = true,
                CreatedAt = DateTime.UtcNow
            });
            await Requirements.SaveChangesAsync();
            return requirement;
        }

        public async Task<Requirement> SeedRequirementWithVerifiedCodeAsync()
        {
            var requirement = new Requirement
            {
                Id = Guid.NewGuid(),
                RequirementNumber = $"REQ-{Guid.NewGuid():N}"[..16],
                Title = "Chamber pressure instrumentation",
                Description = "Sensor must remain within calibrated range during hot-fire.",
                Priority = RequirementPriority.Critical,
                Status = RequirementStatus.Implemented,
                TraceabilityStatus = TraceabilityStatus.PartiallyTraced,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "alice"
            };
            Requirements.Requirements.Add(requirement);
            Requirements.RequirementCodeLinks.Add(new RequirementCodeLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirement.Id,
                CodeFile = "Core/Sensors/ChamberPressure.cs",
                FunctionName = "ReadCalibratedPressure",
                LineStart = 1,
                LineEnd = 40,
                Verified = true,
                CreatedAt = DateTime.UtcNow
            });
            await Requirements.SaveChangesAsync();
            return requirement;
        }

        public async Task SeedCoverageTestAsync(string testCaseId, string testFile)
        {
            var coverage = new CodeCoverage
            {
                Id = Guid.NewGuid(),
                FilePath = "Core/Sensors/ChamberPressure.cs",
                LastUpdated = DateTime.UtcNow
            };
            Coverage.CodeCoverage.Add(coverage);
            Coverage.CoverageTestCaseLinks.Add(new CoverageTestCaseLink
            {
                Id = Guid.NewGuid(),
                CodeCoverageId = coverage.Id,
                TestCaseId = testCaseId,
                TestFile = testFile,
                CoverageType = CoverageType.MCDC,
                CreatedAt = DateTime.UtcNow
            });
            await Coverage.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Reports.DisposeAsync();
            await Requirements.DisposeAsync();
            await Coverage.DisposeAsync();
        }
    }
}
