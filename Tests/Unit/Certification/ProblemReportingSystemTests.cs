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
            ProblemReportStatus.InProgress,
            resolution: null,
            changedBy: "bob-admin");

        var change = await context.ProblemReportStatusChanges.SingleAsync();
        change.OldStatus.Should().Be(ProblemReportStatus.Open);
        change.NewStatus.Should().Be(ProblemReportStatus.InProgress);
        change.ChangedBy.Should().Be("bob-admin");

        var updated = await context.ProblemReports.SingleAsync();
        updated.Status.Should().Be(ProblemReportStatus.InProgress);
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
