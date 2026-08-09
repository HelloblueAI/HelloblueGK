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
    public async Task VerifyTraceabilityAsync_UnverifiedNotRunLinks_AreNotCompliant()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-004",
            Title = "Shutdown interlock",
            Description = "Commanded shutdown must close main valves",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        await system.LinkToDesignAsync(requirement.Id, "DES-004", "DesignDoc.pdf");
        await system.LinkToCodeAsync(requirement.Id, "Core/Valves.cs", 10, 40, "CloseMainValves");
        await system.LinkToTestAsync(requirement.Id, "TC-004", "Tests/ValvesTests.cs", TestCoverageType.MCDC);

        var report = await system.VerifyTraceabilityAsync();

        report.IsCompliant.Should().BeFalse();
        report.Issues.Should().Contain(i =>
            i.Description.Contains("verified design", StringComparison.OrdinalIgnoreCase));
        report.Issues.Should().Contain(i =>
            i.Description.Contains("verified code", StringComparison.OrdinalIgnoreCase));
        report.Issues.Should().Contain(i =>
            i.Description.Contains("verified passed test", StringComparison.OrdinalIgnoreCase));
        report.Issues.Should().Contain(i =>
            i.IssueType == TraceabilityIssueType.MissingMCDCCoverage);
    }

    [Fact]
    public async Task VerifyTraceabilityAsync_VerifiedPassedEvidence_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-005",
            Title = "Propellant sense",
            Description = "Sensor validity gate",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        var design = await system.LinkToDesignAsync(requirement.Id, "DES-005", "DesignDoc.pdf");
        var code = await system.LinkToCodeAsync(requirement.Id, "Core/Sensors.cs", 1, 20, "ValidateSensor");
        var test = await system.LinkToTestAsync(requirement.Id, "TC-005", "Tests/SensorsTests.cs", TestCoverageType.MCDC);

        await system.RecordTestResultAsync(requirement.Id, test.Id, TestResult.Passed);
        await system.VerifyLinkAsync(requirement.Id, design.Id, RequirementLinkKind.Design);
        await system.VerifyLinkAsync(requirement.Id, code.Id, RequirementLinkKind.Code);
        await system.VerifyLinkAsync(requirement.Id, test.Id, RequirementLinkKind.Test);

        var report = await system.VerifyTraceabilityAsync();
        var persisted = await context.Requirements.SingleAsync();

        report.IsCompliant.Should().BeTrue();
        persisted.TraceabilityStatus.Should().Be(TraceabilityStatus.FullyTraced);
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
