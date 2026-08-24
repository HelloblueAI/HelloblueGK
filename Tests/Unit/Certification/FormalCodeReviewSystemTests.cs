using HB_NLP_Research_Lab.Certification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HelloblueGK.Tests.Unit.Certification;

public class FormalCodeReviewSystemTests
{
    [Fact]
    public async Task ApproveReviewAsync_WithoutCompletedCertifiedAssignment_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");

        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Completed*");

        var persisted = await context.CodeReviews.SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.Pending);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task ApproveReviewAsync_WithCompletedCertifiedAssignment_Succeeds()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "Consider naming clarity"
            }
        });

        await system.ApproveReviewAsync(created.Id, "admin");

        var persisted = await context.CodeReviews.SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.Approved);
        persisted.ApprovedBy.Should().Be("admin");
    }

    [Fact]
    public async Task ApproveReviewAsync_WithCriticalFindings_LeavesCompletedNotApproved()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.Safety,
                Description = "blocking critical finding"
            }
        });

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*critical findings*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.ApprovedBy.Should().BeNull();
        persisted.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task ApproveReviewAsync_UsesCompletedStatusClaim()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        await system.AssignReviewerAsync(created.Id, "certified-bob");
        // Assignment completed but review left InProgress — approve must require Completed claim.
        var assignment = await context.CodeReviewAssignments.SingleAsync();
        assignment.Status = ReviewAssignmentStatus.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        created.Status = CodeReviewStatus.InProgress;
        await context.SaveChangesAsync();

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Completed*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.InProgress);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task SubmitFindingsAsync_RejectsAfterApproval()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "nit"
            }
        });
        await system.ApproveReviewAsync(created.Id, "admin");

        var act = async () => await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 6,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.Safety,
                Description = "late critical finding after approve"
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Approved*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.Approved);
    }

    [Fact]
    public async Task SubmitFindingsAsync_RejectsAfterRejection()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");

        created.Status = CodeReviewStatus.Rejected;
        await context.SaveChangesAsync();

        var act = async () => await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 6,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.Safety,
                Description = "late critical finding after reject"
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Rejected*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.Rejected);
        context.ReviewFindings.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignReviewerAsync_ForMissingReview_ThrowsArgumentException()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var assign = async () => await system.AssignReviewerAsync(
            Guid.NewGuid(),
            "certified-bob");

        await assign.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewerNotOnRoster_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        // Client used to forge IsCertified=true; without a roster entry this must fail.
        var assign = async () => await system.AssignReviewerAsync(created.Id, "not-certified");

        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*certified-reviewer roster*");

        context.CodeReviewAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewerRevoked_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RevokeCertifiedReviewerAsync("certified-bob");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        var assign = async () => await system.AssignReviewerAsync(created.Id, "certified-bob");

        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*certified-reviewer roster*");
    }

    [Fact]
    public async Task CreateReviewAsync_AfterFiveDigitSuffix_AllocatesNextNumericSequence()
    {
        await using var context = CreateContext();
        var year = DateTime.UtcNow.Year;
        // Lexicographic OrderByDescending would pick …-9999 over …-10000 and collide.
        context.CodeReviews.AddRange(
            new CodeReview
            {
                Id = Guid.NewGuid(),
                ReviewNumber = $"CR-{year}-9999",
                FilePath = "a.cs",
                FunctionName = "A",
                Author = "alice",
                CreatedAt = DateTime.UtcNow
            },
            new CodeReview
            {
                Id = Guid.NewGuid(),
                ReviewNumber = $"CR-{year}-10000",
                FilePath = "b.cs",
                FunctionName = "B",
                Author = "bob",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "c.cs",
            FunctionName = "C",
            LineStart = 1,
            LineEnd = 2,
            Author = "carol"
        });

        created.ReviewNumber.Should().Be($"CR-{year}-10001");
    }

    [Fact]
    public async Task ApproveReviewAsync_WithUnresolvedMajorFinding_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Major,
                Category = FindingCategory.Correctness,
                Description = "Logic defect"
            }
        });

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved major*");
    }

    [Fact]
    public async Task ApproveReviewAsync_AsAuthor_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>());

        var approve = async () => await system.ApproveReviewAsync(created.Id, "alice");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*independent approver*");
    }

    [Fact]
    public async Task ApproveReviewAsync_AsCompletingReviewer_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>());

        var approve = async () => await system.ApproveReviewAsync(created.Id, "certified-bob");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*separation of duties*");
    }

    [Fact]
    public async Task SubmitFindingsAsync_SafetyCategory_ElevatesSeverityToCritical()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Safety,
                Description = "Possible hazard under-classified as minor"
            }
        });

        var finding = await context.ReviewFindings.SingleAsync();
        finding.Severity.Should().Be(FindingSeverity.Critical);
    }

    [Fact]
    public async Task SubmitFindingsAsync_CriticalKeywords_ElevateClientMinorSeverity()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Correctness,
                Description = "Critical catastrophic failure path left unguarded"
            }
        });

        var finding = await context.ReviewFindings.SingleAsync();
        finding.Severity.Should().Be(FindingSeverity.Critical);

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved critical*");
    }

    [Fact]
    public void ResolveFindingSeverity_SubstringAndNegatedKeywords_DoNotElevate()
    {
        FormalCodeReviewSystem.ResolveFindingSeverity(new ReviewFinding
        {
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Correctness,
            Description = "Change is insignificant, non-critical, and affects a majority of comments"
        }).Should().Be(FindingSeverity.Minor);
    }

    [Fact]
    public void ResolveFindingSeverity_SafetyCriticalCompound_ElevatesToCritical()
    {
        FormalCodeReviewSystem.ResolveFindingSeverity(new ReviewFinding
        {
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Correctness,
            Description = "Missing safety-critical interlock"
        }).Should().Be(FindingSeverity.Critical);
    }

    [Theory]
    [InlineData("Documented hazards remain unmitigated")]
    [InlineData("Hazardous over-temperature path")]
    [InlineData("Fails critically under abort")]
    [InlineData("Catastrophically unbounded recursion")]
    public void ResolveFindingSeverity_DerivedCriticalKeywords_ElevateToCritical(string description)
    {
        FormalCodeReviewSystem.ResolveFindingSeverity(new ReviewFinding
        {
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Correctness,
            Description = description
        }).Should().Be(FindingSeverity.Critical);
    }

    [Fact]
    public void ResolveFindingSeverity_DerivedMajorKeywords_ElevateToMajor()
    {
        FormalCodeReviewSystem.ResolveFindingSeverity(new ReviewFinding
        {
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Correctness,
            Description = "Performance degrades significantly under load"
        }).Should().Be(FindingSeverity.Major);
    }

    [Fact]
    public async Task VerifyComplianceAsync_WithEmptyRequiredFileRoster_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.TotalRequiredFiles.Should().Be(0);
        check.Issues.Should().Contain(i => i.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_UsesServerOwnedRequiredFileRoster()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");
        await system.RegisterRequiredFileAsync("WebAPI/Program.cs", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "nit"
            }
        });
        await system.ApproveReviewAsync(created.Id, "admin");

        var check = await system.VerifyComplianceAsync();

        check.TotalRequiredFiles.Should().Be(2);
        check.ReviewedFiles.Should().Be(1);
        check.IsCompliant.Should().BeFalse();
        check.UnreviewedFiles.Should().ContainSingle().Which.Should().Be("webapi/program.cs");
    }

    [Fact]
    public async Task VerifyComplianceAsync_IgnoresClientCherryPickedScope_WhenRosterHasMoreFiles()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");
        await system.RegisterRequiredFileAsync("Core/EngineSafety.cs", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "nit"
            }
        });
        await system.ApproveReviewAsync(created.Id, "admin");

        // Even if a legacy client would have sent only the approved file, compliance
        // must still fail closed against the full server-owned roster.
        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.UnreviewedFiles.Should().Contain("core/enginesafety.cs");
    }

    [Fact]
    public async Task RegisterRequiredFileAsync_CollapsesCaseVariantDuplicates()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var first = await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");
        var second = await system.RegisterRequiredFileAsync("core/hellobluegkengine.cs", "admin");

        second.Id.Should().Be(first.Id);
        second.FilePath.Should().Be("core/hellobluegkengine.cs");
        context.RequiredReviewFiles.Count(f => f.IsActive).Should().Be(1);

        await system.RevokeRequiredFileAsync("CORE/HelloBlueGKEngine.cs");
        context.RequiredReviewFiles.Single(f => f.Id == first.Id).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyComplianceAsync_NormalizesPathSeparatorsBeforeMatching()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterRequiredFileAsync(" Core/HelloblueGKEngine.cs ", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = @"Core\HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "nit"
            }
        });
        await system.ApproveReviewAsync(created.Id, "admin");

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeTrue();
        check.ReviewedFiles.Should().Be(1);
        check.UnreviewedFiles.Should().BeEmpty();
    }

    private static CodeReviewDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CodeReviewDbContext>()
            .UseSqlite($"Data Source=file:code-reviews-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;

        var context = new CodeReviewDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }
}
