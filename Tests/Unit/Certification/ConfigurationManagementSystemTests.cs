using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class ConfigurationManagementSystemTests
{
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

        var act = async () => await system.ApproveChangeRequestAsync(created.RequestNumber, "Alice", "CCB ok");
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

        await system.ApproveChangeRequestAsync(created.RequestNumber, "bob", "CCB ok");
        await system.ImplementChangeRequestAsync(created.RequestNumber, "alice", new List<Guid>());

        var act = async () => await system.ApproveChangeRequestAsync(created.RequestNumber, "carol", "re-approve");
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
    public async Task PerformAuditAsync_DraftBaselineWithCleanItems_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new ConfigurationManagementSystem(context, NullLogger<ConfigurationManagementSystem>.Instance);

        var baseline = await system.CreateBaselineAsync("Draft-Audit", "0.2.0", "not approved", "alice");
        var item = await system.CreateConfigurationItemAsync(new ConfigurationItem
        {
            ItemName = "core.c",
            ItemType = ConfigurationItemType.SourceCode,
            FilePath = "src/core.c",
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
            FilePath = "src/late.c"
        });

        var act = async () => await system.AddItemToBaselineAsync(baseline.Id, item.Id, "1.0.1");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot accept new configuration items*");
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
            FilePath = "src/late.c",
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
            FilePath = $"src/{itemName}",
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
