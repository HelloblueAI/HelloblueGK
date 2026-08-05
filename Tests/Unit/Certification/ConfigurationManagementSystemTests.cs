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
        await system.ApproveBaselineAsync(baseline.Id, "bob");

        var act = async () => await system.ApproveBaselineAsync(baseline.Id, "carol");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be approved from status Approved*");
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

    private static ConfigurationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseSqlite($"Data Source=file:config-mgmt-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new ConfigurationDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
