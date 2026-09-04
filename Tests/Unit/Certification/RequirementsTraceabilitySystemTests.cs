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

        await system.LinkToDesignAsync(requirement.Id, "DES-004", "Docs/DesignDoc.pdf");
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

        var design = await system.LinkToDesignAsync(requirement.Id, "DES-005", "Docs/DesignDoc.pdf");
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

    [Fact]
    public async Task CreateRequirementAsync_SafetyWording_CannotUnderClassifyPriority()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-SAFE-001",
            Title = "Safety-critical igniter interlock",
            Description = "Must inhibit igniter without propellant flow",
            Priority = RequirementPriority.Medium,
            CreatedBy = "alice"
        });

        requirement.Priority.Should().Be(RequirementPriority.Critical);
        var persisted = await context.Requirements.SingleAsync();
        persisted.Priority.Should().Be(RequirementPriority.Critical);
    }

    [Theory]
    [InlineData(null, "Valve timing", "Main valve open sequence", null, RequirementPriority.Critical)]
    [InlineData("REQ-001", "Valve timing", "Main valve open sequence", RequirementPriority.Medium, RequirementPriority.Medium)]
    [InlineData("REQ-SAFETY-1", "Valve timing", "Main valve open sequence", RequirementPriority.Low, RequirementPriority.Critical)]
    [InlineData("REQ-001", "Chamber pressure limit", "critical overpressure protection", RequirementPriority.High, RequirementPriority.Critical)]
    [InlineData("REQ-001", "Routine housekeeping", "catastrophic failure containment", RequirementPriority.Medium, RequirementPriority.Critical)]
    [InlineData("REQ-001", "Hazardous overpressure relief valve", "Routine observation of valve motion", RequirementPriority.Medium, RequirementPriority.Critical)]
    [InlineData("REQ-001", "Valve timing", "unsafe ignition interlock bypass", RequirementPriority.Low, RequirementPriority.Critical)]
    [InlineData("REQ-001", "Fatal overtemp interlock", "Housekeeping telemetry", RequirementPriority.High, RequirementPriority.Critical)]
    public void ResolvePriority_FailClosedAndKeywordFloor(
        string? number,
        string? title,
        string? description,
        RequirementPriority? explicitPriority,
        RequirementPriority expected)
    {
        RequirementsTraceabilitySystem.ResolvePriority(number, title, description, explicitPriority)
            .Should().Be(expected);
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

    [Theory]
    [InlineData("http://example.test/design.md")]
    [InlineData("https://example.test/design.md")]
    [InlineData("file:///etc/passwd")]
    [InlineData("file:C:/secrets/keys.cs")]
    public async Task LinkToDesignAsync_RejectsSchemeUriEvidencePath(string designDocument)
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-013",
            Title = "URI integrity",
            Description = "Design links must stay in-repo",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToDesignAsync(
            requirement.Id,
            "DE-1",
            designDocument);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*relative*");
    }

    [Fact]
    public async Task CreateRequirementAsync_HazardWording_CannotUnderClassifyPriority()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-HAZ-001",
            Title = "Hazardous overpressure relief valve",
            Description = "Routine observation of valve motion",
            Priority = RequirementPriority.Medium,
            CreatedBy = "alice"
        });

        requirement.Priority.Should().Be(RequirementPriority.Critical);
        var persisted = await context.Requirements.SingleAsync();
        persisted.Priority.Should().Be(RequirementPriority.Critical);
    }

    [Fact]
    public async Task VerifyTraceabilityAsync_LeftoverHazardMedium_RequiresMcdc()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirementId = Guid.NewGuid();
        context.Requirements.Add(new Requirement
        {
            Id = requirementId,
            RequirementNumber = "REQ-HAZ-LEGACY",
            Title = "Hazardous overpressure relief valve",
            Description = "Routine observation of valve motion",
            Priority = RequirementPriority.Medium,
            Status = RequirementStatus.Draft,
            TraceabilityStatus = TraceabilityStatus.NotTraced,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "alice"
        });
        await context.SaveChangesAsync();

        var design = await system.LinkToDesignAsync(requirementId, "DES-HAZ", "Docs/DesignDoc.pdf");
        var code = await system.LinkToCodeAsync(requirementId, "Core/Valves.cs", 1, 20, "RelievePressure");
        var test = await system.LinkToTestAsync(requirementId, "TC-HAZ", "Tests/ValvesTests.cs", TestCoverageType.Statement);

        await system.RecordTestResultAsync(requirementId, test.Id, TestResult.Passed);
        await system.VerifyLinkAsync(requirementId, design.Id, RequirementLinkKind.Design);
        await system.VerifyLinkAsync(requirementId, code.Id, RequirementLinkKind.Code);
        await system.VerifyLinkAsync(requirementId, test.Id, RequirementLinkKind.Test);

        var report = await system.VerifyTraceabilityAsync();

        report.IsCompliant.Should().BeFalse();
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingMCDCCoverage);
    }

    [Fact]
    public async Task LinkToTestAsync_RejectsNonTestsPrefix()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-014",
            Title = "Test namespace",
            Description = "Test links must live under Tests/",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(
            requirement.Id,
            "TC-FORGE",
            "phantom/forge.cs",
            TestCoverageType.MCDC);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Tests/*");
    }

    [Fact]
    public async Task LinkToCodeAsync_RejectsNonImplementationPrefix()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-015",
            Title = "Code namespace",
            Description = "Code links must live under an implementation tree",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToCodeAsync(
            requirement.Id,
            "docs/readme.md",
            1,
            20,
            "Forge");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*code prefix*");
    }

    [Fact]
    public async Task LinkToDesignAsync_RejectsNonDocsPrefix()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-016",
            Title = "Design namespace",
            Description = "Design links must live under Docs/",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToDesignAsync(
            requirement.Id,
            "DE-FORGE",
            "tmp/design.pdf");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Docs/*");
    }

    [Fact]
    public async Task VerifyTraceabilityAsync_PhantomNamespaceLinks_AreNotCompliant()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-017",
            Title = "Legacy phantom evidence",
            Description = "Relative paths outside allowed prefixes must fail closed",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        // Persist pre-gate rows that look "meaningful" (non-whitespace) but point
        // at invented files — the previous HasMeaningful* contract.
        context.RequirementDesignLinks.Add(new RequirementDesignLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            DesignElementId = "DE-PHANTOM",
            DesignDocument = "tmp/design.pdf",
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementCodeLinks.Add(new RequirementCodeLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            CodeFile = "phantom/forge.cs",
            FunctionName = "Forge",
            LineStart = 1,
            LineEnd = 99,
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementTestLinks.Add(new RequirementTestLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            TestCaseId = "TC-PHANTOM",
            TestFile = "phantom/forgeTests.cs",
            CoverageType = TestCoverageType.MCDC,
            TestResult = TestResult.Passed,
            CreatedAt = DateTime.UtcNow,
            Verified = true
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
    public async Task VerifyTraceabilityAsync_LeftoverTraversalLinks_AreNotCompliant()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-018",
            Title = "Legacy traversal evidence",
            Description = "Prefix-qualified traversal must fail closed at verify",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        // Prefix-only leftover checks accepted Docs/../tmp because the string
        // starts with Docs/. LinkTo* already rejects these; seed the forge.
        context.RequirementDesignLinks.Add(new RequirementDesignLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            DesignElementId = "DE-TRAVERSAL",
            DesignDocument = "Docs/../tmp/forge-design.pdf",
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementCodeLinks.Add(new RequirementCodeLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            CodeFile = "Core/../tmp/forge.cs",
            FunctionName = "Forge",
            LineStart = 1,
            LineEnd = 20,
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementTestLinks.Add(new RequirementTestLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            TestCaseId = "TC-TRAVERSAL",
            TestFile = "Tests/../tmp/forgeTests.cs",
            CoverageType = TestCoverageType.MCDC,
            TestResult = TestResult.Passed,
            CreatedAt = DateTime.UtcNow,
            Verified = true
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
    public async Task VerifyTraceabilityAsync_LeftoverSafeNamespaceLinks_AreCompliant()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-019",
            Title = "Legacy in-tree evidence",
            Description = "Matching leftover Docs/Core/Tests paths must still verify",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        context.RequirementDesignLinks.Add(new RequirementDesignLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            DesignElementId = "DE-SAFE",
            DesignDocument = "Docs/DesignDoc.pdf",
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementCodeLinks.Add(new RequirementCodeLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            CodeFile = "Core/Sensors.cs",
            FunctionName = "ValidateSensor",
            LineStart = 1,
            LineEnd = 20,
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementTestLinks.Add(new RequirementTestLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            TestCaseId = "TC-SAFE",
            TestFile = "Tests/SensorsTests.cs",
            CoverageType = TestCoverageType.MCDC,
            TestResult = TestResult.Passed,
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        await context.SaveChangesAsync();

        var report = await system.VerifyTraceabilityAsync();

        report.IsCompliant.Should().BeTrue();
        report.CriticalIssues.Should().Be(0);
    }

    [Theory]
    [InlineData("n/a")]
    [InlineData("none")]
    [InlineData("todo")]
    public async Task VerifyTraceabilityAsync_LeftoverPlaceholderEvidenceIds_AreNotCompliant(
        string leftoverEvidenceId)
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-021",
            Title = "Legacy placeholder evidence ids",
            Description = "Placeholder design/test identity must fail closed at verify",
            Priority = RequirementPriority.Critical,
            CreatedBy = "alice"
        });

        context.RequirementDesignLinks.Add(new RequirementDesignLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            DesignElementId = leftoverEvidenceId,
            DesignDocument = "Docs/DesignDoc.pdf",
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementCodeLinks.Add(new RequirementCodeLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            CodeFile = "Core/Sensors.cs",
            FunctionName = "ValidateSensor",
            LineStart = 1,
            LineEnd = 20,
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        context.RequirementTestLinks.Add(new RequirementTestLink
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            TestCaseId = leftoverEvidenceId,
            TestFile = "Tests/SensorsTests.cs",
            CoverageType = TestCoverageType.MCDC,
            TestResult = TestResult.Passed,
            CreatedAt = DateTime.UtcNow,
            Verified = true
        });
        await context.SaveChangesAsync();

        var report = await system.VerifyTraceabilityAsync();

        report.IsCompliant.Should().BeFalse();
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingDesignLink);
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingTestLink);
        report.Issues.Should().Contain(i => i.IssueType == TraceabilityIssueType.MissingMCDCCoverage);
    }

    [Theory]
    [InlineData("n/a")]
    [InlineData("none")]
    [InlineData("todo")]
    public async Task LinkToDesignAsync_RejectsPlaceholderDesignElementId(string placeholderDesignElementId)
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-022",
            Title = "Reject placeholder design id",
            Description = "Create-time design identity cannot be a placeholder token",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToDesignAsync(
            requirement.Id,
            placeholderDesignElementId,
            "Docs/DesignDoc.pdf");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real identifier*")
            .WithParameterName("designElementId");
        context.RequirementDesignLinks.Should().BeEmpty();
    }

    [Theory]
    [InlineData("n/a")]
    [InlineData("none")]
    [InlineData("todo")]
    public async Task LinkToTestAsync_RejectsPlaceholderTestCaseId(string placeholderTestCaseId)
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-023",
            Title = "Reject placeholder test id",
            Description = "Create-time test identity cannot be a placeholder token",
            CreatedBy = "alice"
        });

        var act = async () => await system.LinkToTestAsync(
            requirement.Id,
            placeholderTestCaseId,
            "Tests/SensorsTests.cs",
            TestCoverageType.MCDC);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real identifier*")
            .WithParameterName("testCaseId");
        context.RequirementTestLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyLinkAsync_RejectsLeftoverTraversalPath()
    {
        await using var context = CreateContext();
        var system = new RequirementsTraceabilitySystem(context, NullLogger<RequirementsTraceabilitySystem>.Instance);

        var requirement = await system.CreateRequirementAsync(new Requirement
        {
            RequirementNumber = "REQ-020",
            Title = "Verify cannot stamp traversal",
            Description = "Leftover Docs/../ paths must not become Verified through the API",
            CreatedBy = "alice"
        });

        var linkId = Guid.NewGuid();
        context.RequirementDesignLinks.Add(new RequirementDesignLink
        {
            Id = linkId,
            RequirementId = requirement.Id,
            DesignElementId = "DE-TRAVERSAL-VERIFY",
            DesignDocument = "Docs/../tmp/forge-design.pdf",
            CreatedAt = DateTime.UtcNow,
            Verified = false
        });
        await context.SaveChangesAsync();

        var act = async () => await system.VerifyLinkAsync(
            requirement.Id,
            linkId,
            RequirementLinkKind.Design);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*vacuous*");
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
