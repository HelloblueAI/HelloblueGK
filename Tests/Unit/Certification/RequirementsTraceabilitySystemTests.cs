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

    [Fact]
    public async Task CreateRequirementAsync_RejectsVacuousNumberTitleOrDescription()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var missingNumber = async () => await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "  ",
            Title = "Chamber pressure limit",
            Description = "Must not exceed design max",
            CreatedBy = "alice"
        });
        await missingNumber.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*number*");

        var missingTitle = async () => await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-EMPTY-TITLE",
            Title = "",
            Description = "Must not exceed design max",
            CreatedBy = "alice"
        });
        await missingTitle.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*title*");

        var missingDescription = async () => await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-EMPTY-DESC",
            Title = "Chamber pressure limit",
            Description = "\t",
            CreatedBy = "alice"
        });
        await missingDescription.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*description*");

        context.Requirements.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkToDesignAsync_RejectsVacuousDesignElement()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-002",
            Title = "Igniter interlock",
            Description = "Must inhibit igniter without propellant flow",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToDesignAsync(requirement.Id, "   ", " ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Design element id is required*");
    }

    [Fact]
    public async Task VerifyTraceabilityAsync_VacuousLinks_DoNotSatisfyTraceability()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-003",
            Title = "Valve timing",
            Description = "Main valve open sequence",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        // Persist whitespace-only links directly to simulate legacy/forged rows.
        context.RequirementDesignLinks.Add(new RequirementDesignLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            DesignElementId = "   ",
            DesignDocument = "",
            CreatedAt = DateTime.UtcNow
        });
        context.RequirementCodeLinks.Add(new RequirementCodeLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            CodeFile = "",
            FunctionName = " ",
            LineStart = 0,
            LineEnd = 0,
            CreatedAt = DateTime.UtcNow
        });
        context.RequirementTestLinks.Add(new RequirementTestLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            TestCaseId = " ",
            TestFile = "",
            CoverageType = TestCoverageType.MCDC,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var report = await system.VerifyTraceabilityAsync();

        report.IsCompliant.Should().BeFalse();
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingDesignLink);
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingCodeLink);
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingTestLink);
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingMCDCCoverage);
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
