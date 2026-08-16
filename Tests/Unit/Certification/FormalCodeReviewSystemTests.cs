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
            .WithMessage("*completed certified reviewer assignment*");

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
    public async Task VerifyComplianceAsync_WithEmptyRequiredFiles_IsNotCompliant()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var check = await system.VerifyComplianceAsync(new List<string>());

        check.IsCompliant.Should().BeFalse();
        check.TotalRequiredFiles.Should().Be(0);
        check.Issues.Should().Contain(i => i.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_CountsOnlyRequiredApprovedIntersection()
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

        var check = await system.VerifyComplianceAsync(new List<string>
        {
            "Core/HelloblueGKEngine.cs",
            "WebAPI/Program.cs"
        });

        check.TotalRequiredFiles.Should().Be(2);
        check.ReviewedFiles.Should().Be(1);
        check.IsCompliant.Should().BeFalse();
        check.UnreviewedFiles.Should().ContainSingle().Which.Should().Be("WebAPI/Program.cs");
    }

    [Fact]
    public async Task VerifyComplianceAsync_NormalizesPathSeparatorsBeforeMatching()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");

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

        var check = await system.VerifyComplianceAsync(new List<string>
        {
            " Core/HelloblueGKEngine.cs "
        });

        check.IsCompliant.Should().BeTrue();
        check.ReviewedFiles.Should().Be(1);
        check.UnreviewedFiles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\system32\\kernel.cs")]
    [InlineData("../secret.cs")]
    [InlineData("Core/../../secret.cs")]
    public async Task CreateReviewAsync_RejectsVacuousOrUnsafeFilePath(string filePath)
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var act = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = filePath,
            FunctionName = "Analyze",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        await act.Should().ThrowAsync<ArgumentException>();
        context.CodeReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReviewAsync_RejectsEmptyFunctionNameAndInvalidLineRange()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var missingFunction = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/Engine.cs",
            FunctionName = "  ",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });
        await missingFunction.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Function name is required*");

        var invalidRange = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/Engine.cs",
            FunctionName = "Ignite",
            LineStart = 10,
            LineEnd = 2,
            Author = "alice"
        });
        await invalidRange.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*line range*");
    }

    [Fact]
    public async Task SubmitFindingsAsync_RejectsEmptyDescription()
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

        var act = async () => await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "   "
            }
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Finding description is required*");
        context.ReviewFindings.Should().BeEmpty();
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
