using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class ConfigurationManagementSystemTests
{
    [Fact]
    public async Task CreateBaselineAsync_RejectsEmptyNameOrVersion()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var missingName = async () => await system.CreateBaselineAsync("  ", "1.0.0", "initial", "alice");
        await missingName.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Baseline name is required*");

        var missingVersion = async () => await system.CreateBaselineAsync("SCI-1", " ", "initial", "alice");
        await missingVersion.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Baseline version is required*");

        context.SoftwareBaselines.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateConfigurationItemAsync_RejectsEmptyNameAndTraversalPath()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var missingName = async () => await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "  ",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "src/core.c"
        });
        await missingName.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ItemName is required*");

        var traversal = async () => await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "../secrets/core.c"
        });
        await traversal.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*traversal*");

        context.ConfigurationItems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://example.test/core.c")]
    [InlineData("https://example.test/core.c")]
    [InlineData("file:///etc/passwd")]
    [InlineData("file:C:/secrets/core.c")]
    public async Task CreateConfigurationItemAsync_RejectsSchemeUriEvidencePath(string filePath)
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var act = async () => await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = filePath
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*relative*");
        context.ConfigurationItems.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateChangeRequestAsync_RejectsEmptyTitle()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var act = async () => await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = " ",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*title is required*");
    }

    [Fact]
    public async Task ApproveBaselineAsync_RejectsNonDraftOrUnderReviewStatus()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("SCI-1", "1.0.0", "initial", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "core.c");
        await system.ApproveBaselineAsync(baseline.Id, "bob");

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "carol");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be approved from status Approved*");
    }

    [Fact]
    public async Task ApproveBaselineAsync_RejectsEmptyBaseline()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Empty-Approve", "0.0.1", "no items", "alice");

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "bob");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*has no configuration items and cannot be approved*");

        var persisted = await context.SoftwareBaselines.AsNoTracking().SingleAsync(b => b.Id == baseline.Id);
        persisted.Status.Should().Be(BaselineStatus.Draft);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task ApproveBaselineAsync_AtomicClaim_RejectsSecondApprover()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("SCI-Race", "1.0.0", "race", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "race.c");
        await system.ApproveBaselineAsync(baseline.Id, "bob");

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "carol");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be approved from status Approved*");

        var persisted = await context.SoftwareBaselines.AsNoTracking().SingleAsync(b => b.Id == baseline.Id);
        persisted.ApprovedBy.Should().Be("bob");
    }

    [Fact]
    public async Task ApproveBaselineAsync_RejectsCreatorAsApprover()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("SoD-Baseline", "1.0.0", "independence", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "sod.c");

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "alice");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*independent approver*");
    }

    [Fact]
    public async Task ApproveChangeRequestAsync_RejectsRequesterAsApprover()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "SoD CR",
            Description = "self-approve forge",
            Justification = "test",
            RequestedBy = "alice"
        });

        var act = async () => await system.ApproveChangeRequestAsync(created.RequestNumber, "Alice", "CCB approved mixture ratio change");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*separation of duties*");
    }

    [Fact]
    public async Task ApproveChangeRequestAsync_RejectsRejectedOrImplementedStatus()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });

        await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "CCB approved mixture ratio change");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "injector.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/injector.c"
        });
        await system.ImplementChangeRequestAsync(created.RequestNumber, "alice", new List<Guid> { item.Id });

        var implemented = await context.ChangeRequests
            .Include(cr => cr.AffectedItems)
            .SingleAsync(cr => cr.Id == created.Id);
        implemented.Status.Should().Be(ChangeRequestStatus.Implemented);
        implemented.ImplementedBy.Should().Be("alice");
        implemented.AffectedItems.Should().ContainSingle()
            .Which.ConfigurationItemId.Should().Be(item.Id);

        var act = async () => await system.ApproveChangeRequestAsync(created.RequestNumber, "carol", "CCB re-approve after implement");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be approved from status Implemented*");
    }

    [Fact]
    public async Task PerformAuditAsync_EmptyBaseline_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Empty", "0.0.1", "no items", "alice");
        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.TotalItems.Should().Be(0);
        audit.IsCompliant.Should().BeFalse();
        audit.Issues.Should().ContainSingle(i => i.IssueType == AuditIssueType.MissingBaseline);
    }

    [Fact]
    public async Task GenerateSCIAsync_RejectsUnapprovedBaseline()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Draft-SCI", "0.1.0", "draft only", "alice");

        var act = async () => await system.GenerateSCIAsync(baseline.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SCI may only be generated for Approved or Released baselines*");
    }

    [Fact]
    public async Task GenerateSCIAsync_SucceedsForApprovedBaseline()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Approved-SCI", "1.0.0", "official", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "sci.c");
        await system.ApproveBaselineAsync(baseline.Id, "bob");

        var sci = await system.GenerateSCIAsync(baseline.Id);
        sci.BaselineName.Should().Be("Approved-SCI");
        sci.Version.Should().Be("1.0.0");
        sci.ConfigurationItems.Should().ContainSingle();
    }

    [Fact]
    public async Task ApproveBaselineAsync_RejectsUnreleasedItemsWithoutChecksum()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Draft-Items", "0.1.0", "unreleased", "alice");
        await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/core.c"
        });
        var item = context.ConfigurationItems.Single();
        await system.AddItemToBaselineAsync(baseline.Id, item.Id, "1.0.0");

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "bob");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Released with a checksum*");

        var persisted = await context.SoftwareBaselines.AsNoTracking().SingleAsync(b => b.Id == baseline.Id);
        persisted.Status.Should().Be(BaselineStatus.Draft);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task GenerateSCIAsync_RejectsApprovedBaselineWithUnreleasedItems()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-Unreleased-SCI", "1.0.0", "legacy unreleased", "alice");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/core.c"
        });
        await system.AddItemToBaselineAsync(baseline.Id, item.Id, "1.0.0");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "bob";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var act = async () => await system.GenerateSCIAsync(baseline.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Released with a checksum*");
    }

    [Fact]
    public async Task GenerateSCIAsync_RejectsApprovedBaselineWithZeroItems()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        // Simulate a legacy Approved empty baseline (pre-gate) still present in the store.
        var baseline = await system.CreateBaselineAsync("Legacy-Empty-SCI", "1.0.0", "legacy empty", "alice");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "bob";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var act = async () => await system.GenerateSCIAsync(baseline.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SCI cannot be generated without configuration evidence*");
    }

    [Fact]
    public async Task PerformAuditAsync_LeftoverApprovedWhitespaceChecksum_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        // Legacy Approved + Released + "   " checksum — Approve/SCI already reject
        // whitespace, but leftover rows previously stamped audit IsCompliant.
        var baseline = await system.CreateBaselineAsync("Legacy-Whitespace-Checksum", "1.0.0", "leftover", "alice");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/core.c",
            Checksum = "   ",
            Size = 128
        });
        item.Status = ConfigurationItemStatus.Released;
        item.Checksum = "   ";
        await context.SaveChangesAsync();
        await system.AddItemToBaselineAsync(baseline.Id, item.Id, "1.0.0");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "bob";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.IsCompliant.Should().BeFalse();
        audit.Issues.Should().Contain(i =>
            i.IssueType == AuditIssueType.MissingChecksum &&
            i.ItemName == "core.c");

        var sci = async () => await system.GenerateSCIAsync(baseline.Id);
        await sci.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Released with a checksum*");
    }

    [Fact]
    public async Task PerformAuditAsync_LeftoverApprovedMatchingChecksum_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-Matching-Checksum", "1.0.0", "leftover ok", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "core.c");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "bob";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.IsCompliant.Should().BeTrue();
        audit.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task PerformAuditAsync_DraftBaselineWithCleanItems_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Draft-Audit", "0.2.0", "not approved", "alice");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/core.c",
            Checksum = "abc123",
            Size = 128,
            Status = ConfigurationItemStatus.Released
        });
        // CreateConfigurationItemAsync forces UnderDevelopment — release it for a clean-item forge attempt.
        item.Status = ConfigurationItemStatus.Released;
        await context.SaveChangesAsync();
        await system.AddItemToBaselineAsync(baseline.Id, item.Id, "1.0.0");

        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.IsCompliant.Should().BeFalse();
        audit.Issues.Should().Contain(i => i.IssueType == AuditIssueType.BaselineNotApproved);
    }

    [Fact]
    public async Task PerformAuditAsync_LeftoverCreatorAsApprover_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-Self-Approve", "1.0.0", "leftover SoD", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "core.c");
        // Approve already rejects creator-as-approver. Stamp leftover Approved + self-approval.
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "Alice";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.IsCompliant.Should().BeFalse();
        audit.Issues.Should().Contain(i => i.IssueType == AuditIssueType.ApprovalNotIndependent);
    }

    [Fact]
    public async Task PerformAuditAsync_LeftoverApprovedWithoutApprover_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-No-Approver", "1.0.0", "leftover empty SoD", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "core.c");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "   ";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.IsCompliant.Should().BeFalse();
        audit.Issues.Should().Contain(i => i.IssueType == AuditIssueType.ApprovalNotIndependent);
    }

    [Fact]
    public async Task PerformAuditAsync_LeftoverIndependentlyApprovedReleasedItems_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-Independent", "1.0.0", "leftover independent", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "core.c");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "bob";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var audit = await system.PerformAuditAsync(baseline.Id);

        audit.IsCompliant.Should().BeTrue();
        audit.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateSCIAsync_RejectsLeftoverCreatorAsApprover()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-Self-SCI", "1.0.0", "leftover SoD SCI", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "sci.c");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "alice";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var act = async () => await system.GenerateSCIAsync(baseline.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*independent approver*");
    }

    [Fact]
    public async Task GenerateSCIAsync_SucceedsForLeftoverIndependentlyApprovedBaseline()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Legacy-Independent-SCI", "1.0.0", "leftover independent SCI", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "sci.c");
        baseline.Status = BaselineStatus.Approved;
        baseline.ApprovedBy = "bob";
        baseline.ApprovedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var sci = await system.GenerateSCIAsync(baseline.Id);
        sci.BaselineName.Should().Be("Legacy-Independent-SCI");
        sci.ConfigurationItems.Should().ContainSingle();
    }

    [Fact]
    public async Task AddItemToBaselineAsync_RejectsApprovedBaseline()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Frozen", "1.0.0", "approved", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "seed.c");
        await system.ApproveBaselineAsync(baseline.Id, "bob");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "late.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/late.c"
        });

        var act = async () => await system.AddItemToBaselineAsync(baseline.Id, item.Id, "1.0.1");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot accept new configuration items*");
    }

    [Fact]
    public async Task CreateChangeRequestAsync_RejectsEmptyJustification()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var act = async () => await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "   ",
            RequestedBy = "alice"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*justification*");
        context.ChangeRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveChangeRequestAsync_RejectsEmptyApprovalNotes()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });

        var act = async () => await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "  ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("approvalNotes");

        var persisted = await context.ChangeRequests.SingleAsync();
        persisted.Status.Should().Be(ChangeRequestStatus.Submitted);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task AddItemToBaselineAsync_RejectsEmptyVersion()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("SCI-2", "1.0.0", "initial", "alice");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/core.c"
        });

        var act = async () => await system.AddItemToBaselineAsync(baseline.Id, item.Id, "   ");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("version");
    }

    [Fact]
    public async Task ImplementChangeRequestAsync_RejectsEmptyAffectedItems()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });
        await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "CCB approved mixture ratio change");

        var act = async () => await system.ImplementChangeRequestAsync(
            created.RequestNumber,
            "alice",
            new List<Guid>());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("affectedItems");

        var persisted = await context.ChangeRequests.SingleAsync();
        persisted.Status.Should().Be(ChangeRequestStatus.Approved);
    }

    [Fact]
    public async Task ImplementChangeRequestAsync_RejectsApproverAsImplementer()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });
        await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "CCB approved mixture ratio change");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "injector.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/injector.c"
        });

        var act = async () => await system.ImplementChangeRequestAsync(
            created.RequestNumber,
            "Bob",
            new List<Guid> { item.Id });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*CCB approver*");

        var persisted = await context.ChangeRequests.SingleAsync();
        persisted.Status.Should().Be(ChangeRequestStatus.Approved);
        persisted.ImplementedBy.Should().BeNull();
    }

    [Fact]
    public async Task ImplementChangeRequestAsync_RejectsEmptyImplementer()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });
        await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "CCB approved mixture ratio change");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "injector.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/injector.c"
        });

        var act = async () => await system.ImplementChangeRequestAsync(
            created.RequestNumber,
            "  ",
            new List<Guid> { item.Id });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("implementedBy");
    }

    [Fact]
    public async Task AddItemToBaselineAsync_RemovesItemWhenBaselineApprovedConcurrently()
    {
        // Shared in-memory DB + two contexts: context A holds a stale Draft tracker entry while
        // context B approves — AddItem must not leave the late item on the Approved baseline.
        var dbName = $"config-mgmt-race-{Guid.NewGuid():N}";
        await using var contextA = CreateSharedContext(dbName);
        await using var contextB = CreateSharedContext(dbName);
        var systemA = new ConfigurationManagementSystem(contextA, NullLogger<ConfigurationManagementSystem>.Instance);
        var systemB = new ConfigurationManagementSystem(contextB, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await systemA.CreateBaselineAsync("Race-Add", "1.0.0", "race", "alice");
        await AddReleasedItemAsync(systemA, contextA, baseline.Id, "seed.c");
        var lateItem = await systemA.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "late.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "Core/late.c",
            Checksum = "late",
            Size = 32
        });
        lateItem.Status = ConfigurationItemStatus.Released;
        await contextA.SaveChangesAsync();

        // Stale Draft snapshot in A's tracker (simulates load-before-approve race).
        _ = await contextA.SoftwareBaselines.FindAsync(baseline.Id);
        await systemB.ApproveBaselineAsync(baseline.Id, "bob");

        var act = async () => await systemA.AddItemToBaselineAsync(baseline.Id, lateItem.Id, "1.0.1");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot accept new configuration items*");

        var itemCount = await contextB.BaselineConfigurationItems
            .AsNoTracking()
            .CountAsync(i => i.BaselineId == baseline.Id);
        itemCount.Should().Be(1);

        var persisted = await contextB.SoftwareBaselines.AsNoTracking().SingleAsync(b => b.Id == baseline.Id);
        persisted.Status.Should().Be(BaselineStatus.Approved);
    }

    [Fact]
    public async Task CreateChangeRequestAsync_AllocatesMonotonicRequestNumbers()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var first = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "CR A",
            Description = "a",
            Justification = "j",
            RequestedBy = "alice"
        });
        var second = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "CR B",
            Description = "b",
            Justification = "j",
            RequestedBy = "alice"
        });

        first.RequestNumber.Should().EndWith("-0001");
        second.RequestNumber.Should().EndWith("-0002");
    }

    [Theory]
    [InlineData("tmp/sci.c")]
    [InlineData("src/core.c")]
    [InlineData("phantom/engine.c")]
    public async Task CreateConfigurationItemAsync_RejectsOutsideTreeEvidencePath(string filePath)
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var act = async () => await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "forged.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = filePath
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*evidence tree*");
        context.ConfigurationItems.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveChangeRequestAsync_RejectsVacuousApprovalNotes()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });

        var act = async () => await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "lgtm");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*substantive*");

        var persisted = await context.ChangeRequests.SingleAsync();
        persisted.Status.Should().Be(ChangeRequestStatus.Submitted);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task CreateBaselineAsync_RejectsPlaceholderCreator()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var act = async () => await system.CreateBaselineAsync("SCI-1", "1.0.0", "initial", "System");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real actor identity*");
        context.SoftwareBaselines.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveBaselineAsync_LeftoverPlaceholderCreator_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("SCI-legacy", "1.0.0", "legacy", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "legacy.c");
        baseline.CreatedBy = "System";
        await context.SaveChangesAsync();

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "bob");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*real creator identity*");

        var persisted = await context.SoftwareBaselines.AsNoTracking().SingleAsync(b => b.Id == baseline.Id);
        persisted.Status.Should().Be(BaselineStatus.Draft);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task ApproveChangeRequestAsync_LeftoverPlaceholderRequester_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var created = await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Legacy CR",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "alice"
        });
        created.RequestedBy = "System";
        await context.SaveChangesAsync();

        var act = async () => await system.ApproveChangeRequestAsync(
            created.RequestNumber,
            "bob",
            "CCB approved mixture ratio change");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*real requester identity*");

        var persisted = await context.ChangeRequests.SingleAsync();
        persisted.Status.Should().Be(ChangeRequestStatus.Submitted);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task GenerateSCIAsync_LeftoverOutsideTreeItem_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("SCI-unsafe", "1.0.0", "legacy path", "alice");
        await AddReleasedItemAsync(system, context, baseline.Id, "safe.c");
        await system.ApproveBaselineAsync(baseline.Id, "bob");

        var unsafeItem = await context.ConfigurationItems.FirstAsync();
        unsafeItem.FilePath = "tmp/forge.c";
        await context.SaveChangesAsync();

        var act = async () => await system.GenerateSCIAsync(baseline.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the repository evidence tree*");
    }

    [Fact]
    public async Task CreateChangeRequestAsync_RejectsPlaceholderRequester()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var act = async () => await system.CreateChangeRequestAsync(new ChangeRequest
        {
            Title = "Update injector map",
            Description = "Adjust mixture ratio schedule",
            Justification = "Stability",
            RequestedBy = "unknown"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real actor identity*");
        context.ChangeRequests.Should().BeEmpty();
    }

    private static async Task AddReleasedItemAsync(
        ConfigurationManagementSystem system,
        ConfigurationDbContext context,
        Guid baselineId,
        string itemName)
    {
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = itemName,
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = $"Core/{itemName}",
            Checksum = "abc123",
            Size = 64
        });
        item.Status = ConfigurationItemStatus.Released;
        await context.SaveChangesAsync();
        await system.AddItemToBaselineAsync(baselineId, item.Id, "1.0.0");
    }

    private static ConfigurationDbContext CreateContext()
        => CreateSharedContext($"config-mgmt-{Guid.NewGuid():N}");

    private static ConfigurationDbContext CreateSharedContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseSqlite($"Data Source=file:{dbName}?mode=memory&cache=shared")
            .Options;

        var context = new ConfigurationDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
