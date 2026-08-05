using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class RequirementsTraceabilitySystemTests
{
    [Fact]
    public async Task VerifyTraceabilityAsync_EmptyRequirements_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var report = await system.VerifyTraceabilityAsync();

        report.TotalRequirements.Should().Be(0);
        report.IsCompliant.Should().BeFalse();
        report.CriticalIssues.Should().BeGreaterThan(0);
        report.Issues.Should().Contain(i =>
            i.Description.Contains("No requirements recorded", StringComparison.Ordinal));
    }

    [Fact]
    public async Task VerifyTraceabilityAsync_MissingLinks_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-001",
            Title = "Chamber pressure limit",
            Description = "Must not exceed design max",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        var report = await system.VerifyTraceabilityAsync();

        report.TotalRequirements.Should().Be(1);
        report.IsCompliant.Should().BeFalse();
        report.CriticalIssues.Should().BeGreaterThan(0);
    }

    private static RequirementsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RequirementsDbContext>()
            .UseSqlite($"Data Source=file:requirements-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new RequirementsDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
