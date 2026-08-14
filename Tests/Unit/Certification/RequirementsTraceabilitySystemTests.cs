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

    [Fact]
    public async Task CreateRequirementAsync_RejectsEmptyNumberAndTitle()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var act = async () => await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "   ",
            Title = "Chamber pressure limit",
            Description = "Must not exceed design max",
            CreatedBy = "alice"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*RequirementNumber is required*");
    }

    [Fact]
    public async Task CreateRequirementAsync_RejectsDuplicateRequirementNumber()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-010",
            Title = "First",
            Description = "Original requirement",
            CreatedBy = "alice"
        });

        var act = async () => await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-010",
            Title = "Duplicate",
            Description = "Same number",
            CreatedBy = "bob"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*already in use*");
    }

    [Fact]
    public async Task LinkToCodeAsync_RejectsParentDirectoryTraversal()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-011",
            Title = "Path integrity",
            Description = "Code links must stay in-repo",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToCodeAsync(
            requirement.Id,
            "../Secrets/keys.cs",
            1,
            10,
            "Forge");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*traversal*");
    }

    [Fact]
    public async Task LinkToTestAsync_RejectsAbsoluteEvidencePath()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-012",
            Title = "Test path integrity",
            Description = "Test links must stay in-repo",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(
            requirement.Id,
            "TC-1",
            "/etc/passwd",
            TestCoverageType.Statement);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*relative*");
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
