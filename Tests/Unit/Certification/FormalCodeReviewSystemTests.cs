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
    public async Task SubmitFindingsAsync_WithEmptyFindings_DoesNotCompleteAssignment()
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

        var empty = async () => await system.SubmitFindingsAsync(
            created.Id,
            "certified-bob",
            new List<ReviewFinding>());

        await empty.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("findings");

        var persisted = await context.CodeReviews
            .Include(r => r.Assignments)
            .SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.InProgress);
        persisted.Assignments.Should().ContainSingle()
            .Which.Status.Should().Be(ReviewAssignmentStatus.Assigned);

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be Completed*");
    }

    [Fact]
    public async Task SubmitFindingsAsync_WithVacuousLineOrDescription_Throws()
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

        var zeroLine = async () => await system.SubmitFindingsAsync(
            created.Id,
            "certified-bob",
            new List<ReviewFinding>
            {
                new()
                {
                    LineNumber = 0,
                    Severity = FindingSeverity.Minor,
                    Category = FindingCategory.Standards,
                    Description = "nit"
                }
            });
        await zeroLine.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("findings");

        var emptyDescription = async () => await system.SubmitFindingsAsync(
            created.Id,
            "certified-bob",
            new List<ReviewFinding>
            {
                new()
                {
                    LineNumber = 4,
                    Severity = FindingSeverity.Minor,
                    Category = FindingCategory.Standards,
                    Description = "   "
                }
            });
        await emptyDescription.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("findings");
    }

    [Fact]
    public async Task ApproveReviewAsync_WithNoFindings_Throws()
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

        var assignment = await context.CodeReviewAssignments.SingleAsync();
        assignment.Status = ReviewAssignmentStatus.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        created.Status = CodeReviewStatus.Completed;
        await context.SaveChangesAsync();

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no findings*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task CreateReviewAsync_WithEmptyFilePath_ThrowsArgumentException()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var create = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "   ",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        await create.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*File path*");
        context.CodeReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReviewAsync_WithParentDirectorySegment_ThrowsArgumentException()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var create = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "../Secrets/keys.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice"
        });

        await create.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*traversal*");
        context.CodeReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewApproved_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterCertifiedReviewerAsync("certified-carol", "admin");

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

        var assign = async () => await system.AssignReviewerAsync(created.Id, "certified-carol");

        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Approved*");

        var persisted = await context.CodeReviews
            .Include(r => r.Assignments)
            .SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.Approved);
        persisted.Assignments.Should().ContainSingle();
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewRejected_Throws()
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

        created.Status = CodeReviewStatus.Rejected;
        await context.SaveChangesAsync();

        var assign = async () => await system.AssignReviewerAsync(created.Id, "certified-bob");

        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Rejected*");
        context.CodeReviewAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewCompleted_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterCertifiedReviewerAsync("certified-carol", "admin");

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

        var assign = async () => await system.AssignReviewerAsync(created.Id, "certified-carol");
        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Completed*");

        var persisted = await context.CodeReviews
            .Include(r => r.Assignments)
            .SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.Assignments.Should().ContainSingle();
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
            .WithMessage("*unresolved critical*");

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
    public async Task AssignReviewerAsync_AfterCompleted_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterCertifiedReviewerAsync("certified-carol", "admin");

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

        var assign = async () => await system.AssignReviewerAsync(created.Id, "certified-carol");
        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Completed*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        context.CodeReviewAssignments.Should().HaveCount(1);
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
    public async Task AssignReviewerAsync_AfterApproved_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterCertifiedReviewerAsync("certified-carol", "admin");

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

        var assign = async () => await system.AssignReviewerAsync(created.Id, "certified-carol");
        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Approved*");

        context.CodeReviewAssignments.Should().HaveCount(1);
    }

    [Fact]
    public async Task SubmitFindingsAsync_RejectsAfterCompleted()
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

        var completed = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        completed.Status.Should().Be(CodeReviewStatus.Completed);

        var act = async () => await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 6,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.Safety,
                Description = "late critical finding after completed"
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Completed*");

        var persisted = await context.CodeReviews
            .Include(r => r.Findings)
            .AsNoTracking()
            .SingleAsync(r => r.Id == created.Id);
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.Findings.Should().ContainSingle();
        persisted.ApprovedBy.Should().BeNull();
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
            FilePath = "Core/c.cs",
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
    public async Task SubmitFindingsAsync_UnsafeOrFatalKeywords_ElevateClientMinorSeverity()
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
                Description = "Unsafe overpressure relief with a fatal shutdown path"
            }
        });

        var finding = await context.ReviewFindings.SingleAsync();
        finding.Severity.Should().Be(FindingSeverity.Critical);

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved critical*");
    }

    [Fact]
    public async Task ApproveReviewAsync_LeftoverUnsafeMinorFinding_RescoresAndBlocks()
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

        var assignment = await context.CodeReviewAssignments.SingleAsync();
        assignment.Status = ReviewAssignmentStatus.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        created.Status = CodeReviewStatus.Completed;
        context.ReviewFindings.Add(new ReviewFinding
        {
            Id = Guid.NewGuid(),
            ReviewId = created.Id,
            ReviewerName = "certified-bob",
            LineNumber = 5,
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Correctness,
            Description = "Unsafe overpressure relief valve",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var approve = async () => await system.ApproveReviewAsync(created.Id, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved critical*");

        var persistedFinding = await context.ReviewFindings.AsNoTracking().SingleAsync();
        persistedFinding.Severity.Should().Be(FindingSeverity.Critical);

        var persistedReview = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == created.Id);
        persistedReview.Status.Should().Be(CodeReviewStatus.Completed);
        persistedReview.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public void ResolveFindingSeverity_SubstringAndNegatedKeywords_DoNotElevate()
    {
        FormalCodeReviewSystem.ResolveFindingSeverity(new ReviewFinding
        {
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Correctness,
            Description = "Change is insignificant, non-critical, non-fatal, and affects a majority of comments"
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
    [InlineData("Unsafe overpressure relief valve")]
    [InlineData("Unsafely disabled interlock")]
    [InlineData("Fatal engine shutdown path")]
    [InlineData("Fatally unbounded recursion")]
    [InlineData("Documented fatalities remain unmitigated")]
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
    public async Task RegisterRequiredFileAsync_RejectsTraversalAndAbsolutePaths()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var traversal = async () => await system.RegisterRequiredFileAsync("../secrets/core.c", "admin");
        await traversal.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath")
            .WithMessage("*traversal*");

        var absolute = async () => await system.RegisterRequiredFileAsync("/etc/passwd", "admin");
        await absolute.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("filePath")
            .WithMessage("*relative*");

        context.RequiredReviewFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyComplianceAsync_LegacyTraversalRosterAndApproval_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        context.RequiredReviewFiles.Add(new RequiredReviewFile
        {
            Id = Guid.NewGuid(),
            FilePath = "../secrets/core.c",
            IsActive = true,
            RegisteredBy = "admin",
            RegisteredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Leftover rows (pre-path-hardening) can already be Approved. CreateReviewAsync
        // now rejects traversal, so forge the review the same way the roster was forged.
        context.CodeReviews.Add(new CodeReview
        {
            Id = Guid.NewGuid(),
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-legacy-traversal",
            FilePath = "../secrets/core.c",
            FunctionName = "Leak",
            LineStart = 1,
            LineEnd = 2,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.UnreviewedFiles.Should().Contain("../secrets/core.c");

        await system.RevokeRequiredFileAsync("../Secrets/core.c");
        context.RequiredReviewFiles.Single().IsActive.Should().BeFalse();

        var afterRevoke = await system.VerifyComplianceAsync();
        afterRevoke.IsCompliant.Should().BeFalse();
        afterRevoke.Issues.Should().Contain(i =>
            i.Contains("Required file roster is empty", StringComparison.Ordinal));
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

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverApprovedWithUnresolvedCriticalFinding_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        SeedLeftoverApprovedReview(
            context,
            filePath: "core/hellobluegkengine.cs",
            finding: new ReviewFinding
            {
                Id = Guid.NewGuid(),
                ReviewerName = "certified-bob",
                LineNumber = 5,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.Safety,
                Description = "unresolved catastrophic abort path",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle()
            .Which.Should().Be("core/hellobluegkengine.cs");
        check.Issues.Should().Contain(i =>
            i.Contains("undispositioned", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverApprovedMinorWithCatastrophicLanguage_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        SeedLeftoverApprovedReview(
            context,
            filePath: "core/hellobluegkengine.cs",
            finding: new ReviewFinding
            {
                Id = Guid.NewGuid(),
                ReviewerName = "certified-bob",
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Correctness,
                Description = "routine observation of catastrophic engine failure",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle()
            .Which.Should().Be("core/hellobluegkengine.cs");
        check.Issues.Should().Contain(i =>
            i.Contains("undispositioned", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverApprovedCriticalWithDisposition_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        SeedLeftoverApprovedReview(
            context,
            filePath: "core/hellobluegkengine.cs",
            finding: new ReviewFinding
            {
                Id = Guid.NewGuid(),
                ReviewerName = "certified-bob",
                LineNumber = 5,
                Severity = FindingSeverity.Critical,
                Category = FindingCategory.Safety,
                Description = "catastrophic abort path documented",
                Resolved = true,
                ResolvedBy = "admin",
                ResolvedAt = DateTime.UtcNow,
                Resolution = "Mitigated by independent abort interlock review",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeTrue();
        check.ReviewedFiles.Should().Be(1);
        check.UnreviewedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateSummaryAsync_LeftoverMinorWithCatastrophicLanguage_CountsAsCritical()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        SeedLeftoverApprovedReview(
            context,
            filePath: "core/hellobluegkengine.cs",
            finding: new ReviewFinding
            {
                Id = Guid.NewGuid(),
                ReviewerName = "certified-bob",
                LineNumber = 5,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Correctness,
                Description = "routine observation of catastrophic engine failure",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var summary = await system.GenerateSummaryAsync();

        summary.TotalFindings.Should().Be(1);
        summary.CriticalFindings.Should().Be(1);
        summary.MinorFindings.Should().Be(0);
    }

    [Fact]
    public async Task ResolveFindingAsync_WithoutSubstantiveNotes_LeavesFindingBlocking()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var (reviewId, findingId) = await SeedCompletedReviewWithCriticalFindingAsync(system, context);

        var empty = async () => await system.ResolveFindingAsync(reviewId, findingId, "admin", "   ");
        await empty.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*substantive notes*");

        var vacuous = async () => await system.ResolveFindingAsync(reviewId, findingId, "admin", "fixed");
        await vacuous.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*substantive notes*");

        var shortNote = async () => await system.ResolveFindingAsync(reviewId, findingId, "admin", "ok closed");
        await shortNote.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*substantive notes*");

        var punctuationOnly = async () => await system.ResolveFindingAsync(reviewId, findingId, "admin", "............");
        await punctuationOnly.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*substantive notes*");

        var digitsOnly = async () => await system.ResolveFindingAsync(reviewId, findingId, "admin", "123456789012");
        await digitsOnly.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*substantive notes*");

        var finding = await context.ReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
        finding.Resolved.Should().BeFalse();
        finding.Resolution.Should().BeNull();

        var approve = async () => await system.ApproveReviewAsync(reviewId, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved critical*");
    }

    [Fact]
    public async Task ResolveFindingAsync_WithSubstantiveNotes_AllowsIndependentApprove()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var (reviewId, findingId) = await SeedCompletedReviewWithCriticalFindingAsync(system, context);

        await system.ResolveFindingAsync(
            reviewId,
            findingId,
            "admin",
            "Mitigated by adding the missing interlock and retesting the abort path.");

        var finding = await context.ReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
        finding.Resolved.Should().BeTrue();
        finding.ResolvedBy.Should().Be("admin");
        finding.Resolution.Should().Contain("interlock");

        await system.ApproveReviewAsync(reviewId, "admin");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        persisted.Status.Should().Be(CodeReviewStatus.Approved);
        persisted.ApprovedBy.Should().Be("admin");
    }

    [Fact]
    public async Task ApproveReviewAsync_TreatsResolvedWithoutNotesAsUnresolved()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var (reviewId, findingId) = await SeedCompletedReviewWithCriticalFindingAsync(system, context);

        var finding = await context.ReviewFindings.SingleAsync(f => f.Id == findingId);
        finding.Resolved = true;
        finding.ResolvedAt = DateTime.UtcNow;
        finding.Resolution = "ok";
        await context.SaveChangesAsync();

        var approve = async () => await system.ApproveReviewAsync(reviewId, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved critical*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task ApproveReviewAsync_TreatsPunctuationOnlyResolutionAsUnresolved()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var (reviewId, findingId) = await SeedCompletedReviewWithCriticalFindingAsync(system, context);

        var finding = await context.ReviewFindings.SingleAsync(f => f.Id == findingId);
        finding.Resolved = true;
        finding.ResolvedAt = DateTime.UtcNow;
        finding.ResolvedBy = "legacy";
        finding.Resolution = "............";
        await context.SaveChangesAsync();

        var approve = async () => await system.ApproveReviewAsync(reviewId, "admin");
        await approve.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unresolved critical*");

        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task ResolveFindingAsync_CanAttachNotesToVacuousResolvedFlag()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        var (reviewId, findingId) = await SeedCompletedReviewWithCriticalFindingAsync(system, context);

        var forged = await context.ReviewFindings.SingleAsync(f => f.Id == findingId);
        forged.Resolved = true;
        forged.ResolvedAt = DateTime.UtcNow;
        forged.Resolution = "ok";
        await context.SaveChangesAsync();

        await system.ResolveFindingAsync(
            reviewId,
            findingId,
            "admin",
            "Mitigated by adding the missing interlock and retesting the abort path.");

        var finding = await context.ReviewFindings.AsNoTracking().SingleAsync(f => f.Id == findingId);
        finding.Resolved.Should().BeTrue();
        finding.ResolvedBy.Should().Be("admin");
        finding.Resolution.Should().Contain("interlock");

        await system.ApproveReviewAsync(reviewId, "admin");
        var persisted = await context.CodeReviews.AsNoTracking().SingleAsync(r => r.Id == reviewId);
        persisted.Status.Should().Be(CodeReviewStatus.Approved);
    }

    [Fact]
    public async Task ResolveFindingAsync_AfterApproval_Throws()
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

        var findingId = (await context.ReviewFindings.SingleAsync()).Id;
        var act = async () => await system.ResolveFindingAsync(
            created.Id,
            findingId,
            "admin",
            "Post-approve rewrite of a frozen review finding.");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Approved*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\system32\\kernel.cs")]
    [InlineData("../secret.cs")]
    [InlineData("Core/../../secret.cs")]
    [InlineData("tmp/forge.cs")]
    [InlineData("src/engine.cs")]
    [InlineData("a.cs")]
    [InlineData("http://example.test/Core/Engine.cs")]
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


    [Fact]
    public async Task RegisterRequiredFileAsync_RejectsOutsideTreePath()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var act = async () => await system.RegisterRequiredFileAsync("tmp/forge.cs", "admin");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*implementation or test tree*");
        context.RequiredReviewFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverOutsideTreeRoster_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        context.RequiredReviewFiles.Add(new RequiredReviewFile
        {
            Id = Guid.NewGuid(),
            FilePath = "tmp/forge.cs",
            IsActive = true,
            RegisteredBy = "admin",
            RegisteredAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.Issues.Should().Contain(i => i.Contains("outside the implementation or test tree", StringComparison.OrdinalIgnoreCase));
        check.UnreviewedFiles.Should().Contain("tmp/forge.cs");
    }

    [Fact]
    public async Task RegisterCertifiedReviewerAsync_RejectsPlaceholderName()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var act = async () => await system.RegisterCertifiedReviewerAsync("System", "admin");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real actor identity*");
        context.CertifiedReviewers.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReviewAsync_RejectsPlaceholderAuthor()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var act = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "System"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real actor identity*");
        context.CodeReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateReviewAsync_RejectsEmptyAuthor()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);

        var act = async () => await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/Engine.cs",
            FunctionName = "Ignite",
            LineStart = 1,
            LineEnd = 10,
            Author = "  "
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Author is required*");
        context.CodeReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewerIsAuthor_Throws()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("alice", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "Alice"
        });

        var assign = async () => await system.AssignReviewerAsync(created.Id, "alice");
        await assign.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*independent reviewer*");

        var persisted = await context.CodeReviews
            .Include(r => r.Assignments)
            .SingleAsync();
        persisted.Assignments.Should().BeEmpty();
        persisted.Status.Should().Be(CodeReviewStatus.Pending);
    }

    [Fact]
    public async Task AssignReviewerAsync_WhenReviewerAlreadyAssigned_Throws()
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

        var assignAgain = async () => await system.AssignReviewerAsync(created.Id, "certified-bob");
        await assignAgain.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already assigned*");

        var persisted = await context.CodeReviewAssignments.CountAsync();
        persisted.Should().Be(1);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("LGTM")]
    [InlineData("fine")]
    [InlineData("looks good")]
    [InlineData("no issues")]
    public async Task SubmitFindingsAsync_WithVacuousDescriptionToken_Throws(string description)
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
                Description = description
            }
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("findings")
            .WithMessage("*substantive*");

        var persisted = await context.CodeReviews
            .Include(r => r.Assignments)
            .Include(r => r.Findings)
            .SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.InProgress);
        persisted.Assignments.Should().ContainSingle()
            .Which.Status.Should().Be(ReviewAssignmentStatus.Assigned);
        persisted.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitFindingsAsync_RejectsLineNumberOutsideReviewSpan()
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
                LineNumber = 99,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "Comment cites a line that was never in the review span"
            }
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*line range*");
        context.ReviewFindings.Should().BeEmpty();
        var persisted = await context.CodeReviews
            .Include(r => r.Assignments)
            .SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.InProgress);
        persisted.Assignments.Single().Status.Should().Be(ReviewAssignmentStatus.Assigned);
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverApprovedWithoutCertifiedFindings_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        context.CodeReviews.Add(new CodeReview
        {
            Id = Guid.NewGuid(),
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-0099",
            FilePath = "core/hellobluegkengine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Author = "alice",
            Status = CodeReviewStatus.Approved,
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle()
            .Which.Should().Be("core/hellobluegkengine.cs");
    }

    [Fact]
    public async Task VerifyComplianceAsync_OneLineApprovedReview_DoesNotSatisfyRoster()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterRequiredFileAsync("Core/FlightControl.cs", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/FlightControl.cs",
            FunctionName = "noop",
            LineStart = 1,
            LineEnd = 1,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 1,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "nit"
            }
        });
        await system.ApproveReviewAsync(created.Id, "admin");

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle().Which.Should().Be("core/flightcontrol.cs");
        check.Issues.Should().Contain(i => i.Contains("file-covering", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverAuthorAsApprover_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        // Approve already rejects author-as-approver. A leftover Approved row with
        // Author == ApprovedBy (case-insensitive) previously stamped IsCompliant.
        context.CodeReviews.Add(new CodeReview
        {
            Id = Guid.NewGuid(),
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9101",
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "Alice",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle().Which.Should().Be("core/hellobluegkengine.cs");
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverCompletingReviewerAsApprover_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        var reviewId = Guid.NewGuid();
        context.CodeReviews.Add(new CodeReview
        {
            Id = reviewId,
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9102",
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "bob",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        context.CodeReviewAssignments.Add(new CodeReviewAssignment
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReviewerName = "bob",
            IsCertified = true,
            Status = ReviewAssignmentStatus.Completed,
            AssignedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle().Which.Should().Be("core/hellobluegkengine.cs");
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverApprovedWithoutApprover_FailsClosed()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        context.CodeReviews.Add(new CodeReview
        {
            Id = Guid.NewGuid(),
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9103",
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = null,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverIndependentlyApprovedFileCoveringReview_IsCompliant()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/HelloblueGKEngine.cs", "admin");

        var reviewId = Guid.NewGuid();
        context.CodeReviews.Add(new CodeReview
        {
            Id = reviewId,
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9104",
            FilePath = "Core/HelloblueGKEngine.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        context.CodeReviewAssignments.Add(new CodeReviewAssignment
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReviewerName = "certified-bob",
            IsCertified = true,
            Status = ReviewAssignmentStatus.Completed,
            AssignedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        context.ReviewFindings.Add(new ReviewFinding
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReviewerName = "certified-bob",
            LineNumber = 5,
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Standards,
            Description = "Consider naming clarity",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeTrue();
        check.ReviewedFiles.Should().Be(1);
        check.UnreviewedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyComplianceAsync_InteriorSpanApprovedReview_DoesNotSatisfyRoster()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterCertifiedReviewerAsync("certified-bob", "admin");
        await system.RegisterRequiredFileAsync("Core/FlightControl.cs", "admin");

        var created = await system.CreateReviewAsync(new CodeReview
        {
            FilePath = "Core/FlightControl.cs",
            FunctionName = "HandleTick",
            LineStart = 90,
            LineEnd = 99,
            Author = "alice"
        });
        await system.AssignReviewerAsync(created.Id, "certified-bob");
        await system.SubmitFindingsAsync(created.Id, "certified-bob", new List<ReviewFinding>
        {
            new()
            {
                LineNumber = 92,
                Severity = FindingSeverity.Minor,
                Category = FindingCategory.Standards,
                Description = "nit"
            }
        });
        await system.ApproveReviewAsync(created.Id, "admin");

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.Issues.Should().Contain(i => i.Contains("file-covering approved span", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyComplianceAsync_LeftoverApprovedReviewWithoutFunctionName_DoesNotSatisfyRoster(
        string leftoverFunctionName)
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/FlightControl.cs", "admin");

        // CreateReviewAsync now rejects empty FunctionName. Seed a leftover Approved
        // file-covering row the same way a pre-hardening database would.
        context.CodeReviews.Add(new CodeReview
        {
            Id = Guid.NewGuid(),
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9101",
            FilePath = "Core/FlightControl.cs",
            FunctionName = leftoverFunctionName,
            LineStart = 1,
            LineEnd = FormalCodeReviewSystem.MinimumFileReviewLineCount,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeFalse();
        check.ReviewedFiles.Should().Be(0);
        check.UnreviewedFiles.Should().ContainSingle().Which.Should().Be("core/flightcontrol.cs");
        check.Issues.Should().Contain(i => i.Contains("named function", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyComplianceAsync_LeftoverApprovedReviewWithNamedFunction_SatisfiesRoster()
    {
        await using var context = CreateContext();
        var system = new FormalCodeReviewSystem(context, NullLogger<FormalCodeReviewSystem>.Instance);
        await system.RegisterRequiredFileAsync("Core/FlightControl.cs", "admin");

        var reviewId = Guid.NewGuid();
        context.CodeReviews.Add(new CodeReview
        {
            Id = reviewId,
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9102",
            FilePath = "Core/FlightControl.cs",
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = FormalCodeReviewSystem.MinimumFileReviewLineCount,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        context.CodeReviewAssignments.Add(new CodeReviewAssignment
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReviewerName = "certified-bob",
            IsCertified = true,
            Status = ReviewAssignmentStatus.Completed,
            AssignedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        context.ReviewFindings.Add(new ReviewFinding
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReviewerName = "certified-bob",
            LineNumber = 5,
            Severity = FindingSeverity.Minor,
            Category = FindingCategory.Standards,
            Description = "Consider naming clarity",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var check = await system.VerifyComplianceAsync();

        check.IsCompliant.Should().BeTrue();
        check.ReviewedFiles.Should().Be(1);
        check.UnreviewedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveReviewAsync_RejectsPlaceholderApprover()
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

        var act = async () => await system.ApproveReviewAsync(created.Id, "System");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*real actor identity*");

        var persisted = await context.CodeReviews.SingleAsync();
        persisted.Status.Should().Be(CodeReviewStatus.Completed);
        persisted.ApprovedBy.Should().BeNull();
    }

    private static async Task<(Guid ReviewId, Guid FindingId)> SeedCompletedReviewWithCriticalFindingAsync(
        FormalCodeReviewSystem system,
        CodeReviewDbContext context)
    {
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

        var findingId = await context.ReviewFindings
            .Where(f => f.ReviewId == created.Id)
            .Select(f => f.Id)
            .SingleAsync();
        return (created.Id, findingId);
    }

    private static void SeedLeftoverApprovedReview(
        CodeReviewDbContext context,
        string filePath,
        ReviewFinding finding)
    {
        var reviewId = Guid.NewGuid();
        finding.ReviewId = reviewId;

        context.CodeReviews.Add(new CodeReview
        {
            Id = reviewId,
            ReviewNumber = $"CR-{DateTime.UtcNow.Year}-9002",
            FilePath = filePath,
            FunctionName = "AnalyzeEngineAsync",
            LineStart = 1,
            LineEnd = 10,
            Status = CodeReviewStatus.Approved,
            Author = "alice",
            ApprovedBy = "admin",
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        context.CodeReviewAssignments.Add(new CodeReviewAssignment
        {
            Id = Guid.NewGuid(),
            ReviewId = reviewId,
            ReviewerName = "certified-bob",
            AssignedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = ReviewAssignmentStatus.Completed,
            IsCertified = true
        });
        context.ReviewFindings.Add(finding);
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
