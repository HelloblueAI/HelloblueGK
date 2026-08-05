using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class ProblemReportingSystemTests
{
    [Fact]
    public async Task UpdateStatusAsync_RecordsPreviousStatusAndChangedBy()
    {
        await using var context = CreateContext();
        var system = new ProblemReportingSystem(context, NullLogger<ProblemReportingSystem>.Instance);

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

        var change = await context.ProblemReportStatusChanges.SingleAsync();
        change.OldStatus.Should().Be(ProblemReportStatus.Open);
        change.NewStatus.Should().Be(ProblemReportStatus.UnderInvestigation);
        change.ChangedBy.Should().Be("bob-admin");

        var updated = await context.ProblemReports.SingleAsync();
        updated.Status.Should().Be(ProblemReportStatus.UnderInvestigation);
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsOpenToClosedForge()
    {
        await using var context = CreateContext();
        var system = new ProblemReportingSystem(context, NullLogger<ProblemReportingSystem>.Instance);

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
        await using var context = CreateContext();
        var system = new ProblemReportingSystem(context, NullLogger<ProblemReportingSystem>.Instance);

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
        await using var context = CreateContext();
        var system = new ProblemReportingSystem(context, NullLogger<ProblemReportingSystem>.Instance);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("No problem reports recorded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyComplianceAsync_ProperlyClosedCritical_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new ProblemReportingSystem(context, NullLogger<ProblemReportingSystem>.Instance);

        var created = await system.CreateProblemReportAsync(new ProblemReport
        {
            Title = "Critical sensor fault",
            Description = "Chamber pressure sensor stuck",
            Impact = "critical safety instrumentation fault",
            ReportedBy = "alice"
        });

        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.UnderInvestigation, changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Resolved, resolution: "replaced sensor", changedBy: "bob");
        await system.UpdateStatusAsync(created.ReportNumber, ProblemReportStatus.Closed, resolution: "verified on stand", changedBy: "bob");

        var check = await system.VerifyComplianceAsync();
        check.IsCompliant.Should().BeTrue();
        check.UnresolvedCriticalProblems.Should().Be(0);
    }

    private static ProblemReportDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ProblemReportDbContext>()
            .UseSqlite($"Data Source=file:problem-reports-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new ProblemReportDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
