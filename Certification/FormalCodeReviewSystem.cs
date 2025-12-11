using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HB_NLP_Research_Lab.Certification
{
    /// <summary>
    /// Formal Code Review System for DO-178C Level A / NASA NPR 7150.2 Class A
    /// Tracks all code reviews with certified reviewers
    /// </summary>
    public class FormalCodeReviewSystem
    {
        private readonly CodeReviewDbContext _context;
        private readonly ILogger<FormalCodeReviewSystem> _logger;

        public FormalCodeReviewSystem(CodeReviewDbContext context, ILogger<FormalCodeReviewSystem> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a formal code review
        /// </summary>
        public async Task<CodeReview> CreateReviewAsync(CodeReview review)
        {
            // Generate review number
            var year = DateTime.UtcNow.Year;
            var existingCount = await _context.CodeReviews
                .CountAsync(cr => cr.ReviewNumber.StartsWith($"CR-{year}-"));
            
            review.ReviewNumber = $"CR-{year}-{(existingCount + 1):D4}";
            review.Id = Guid.NewGuid();
            review.CreatedAt = DateTime.UtcNow;
            review.Status = CodeReviewStatus.Pending;

            _context.CodeReviews.Add(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created code review {ReviewNumber} for {FilePath}", review.ReviewNumber, review.FilePath);
            return review;
        }

        /// <summary>
        /// Assign reviewer to code review
        /// </summary>
        public async Task AssignReviewerAsync(Guid reviewId, string reviewerName, bool isCertified)
        {
            if (!isCertified)
                throw new InvalidOperationException("Reviewer must be certified for Level A reviews");

            var assignment = new CodeReviewAssignment
            {
                Id = Guid.NewGuid(),
                ReviewId = reviewId,
                ReviewerName = reviewerName,
                IsCertified = isCertified,
                AssignedAt = DateTime.UtcNow,
                Status = ReviewAssignmentStatus.Assigned
            };

            _context.CodeReviewAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Assigned certified reviewer {ReviewerName} to review {ReviewId}", reviewerName, reviewId);
        }

        /// <summary>
        /// Submit review findings
        /// </summary>
        public async Task SubmitFindingsAsync(Guid reviewId, string reviewerName, List<ReviewFinding> findings)
        {
            var review = await _context.CodeReviews
                .Include(r => r.Assignments)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                throw new ArgumentException($"Review {reviewId} not found");

            var assignment = review.Assignments.FirstOrDefault(a => a.ReviewerName == reviewerName);
            if (assignment == null)
                throw new ArgumentException($"Reviewer {reviewerName} not assigned to review {reviewId}");

            foreach (var finding in findings)
            {
                finding.Id = Guid.NewGuid();
                finding.ReviewId = reviewId;
                finding.ReviewerName = reviewerName;
                finding.CreatedAt = DateTime.UtcNow;
                _context.ReviewFindings.Add(finding);
            }

            assignment.Status = ReviewAssignmentStatus.Completed;
            assignment.CompletedAt = DateTime.UtcNow;

            // Check if all reviewers have completed
            var allCompleted = review.Assignments.All(a => a.Status == ReviewAssignmentStatus.Completed);
            if (allCompleted)
            {
                review.Status = CodeReviewStatus.Completed;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Submitted {Count} findings for review {ReviewNumber}", findings.Count, review.ReviewNumber);
        }

        /// <summary>
        /// Approve code review
        /// </summary>
        public async Task ApproveReviewAsync(Guid reviewId, string approvedBy)
        {
            var review = await _context.CodeReviews
                .Include(r => r.Findings)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                throw new ArgumentException($"Review {reviewId} not found");

            // Check for critical findings
            var criticalFindings = review.Findings.Where(f => f.Severity == FindingSeverity.Critical).ToList();
            if (criticalFindings.Any())
                throw new InvalidOperationException($"Cannot approve review with {criticalFindings.Count} critical findings");

            review.Status = CodeReviewStatus.Approved;
            review.ApprovedBy = approvedBy;
            review.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Approved code review {ReviewNumber}", review.ReviewNumber);
        }

        /// <summary>
        /// Generate code review summary
        /// </summary>
        public async Task<CodeReviewSummary> GenerateSummaryAsync()
        {
            var reviews = await _context.CodeReviews
                .Include(r => r.Assignments)
                .Include(r => r.Findings)
                .ToListAsync();

            var summary = new CodeReviewSummary
            {
                GeneratedAt = DateTime.UtcNow,
                TotalReviews = reviews.Count,
                PendingReviews = reviews.Count(r => r.Status == CodeReviewStatus.Pending),
                InProgress = reviews.Count(r => r.Status == CodeReviewStatus.InProgress),
                Completed = reviews.Count(r => r.Status == CodeReviewStatus.Completed),
                Approved = reviews.Count(r => r.Status == CodeReviewStatus.Approved),
                Rejected = reviews.Count(r => r.Status == CodeReviewStatus.Rejected),
                TotalFindings = reviews.Sum(r => r.Findings.Count),
                CriticalFindings = reviews.Sum(r => r.Findings.Count(f => f.Severity == FindingSeverity.Critical)),
                MajorFindings = reviews.Sum(r => r.Findings.Count(f => f.Severity == FindingSeverity.Major)),
                MinorFindings = reviews.Sum(r => r.Findings.Count(f => f.Severity == FindingSeverity.Minor))
            };

            return summary;
        }

        /// <summary>
        /// Verify all code has been reviewed
        /// </summary>
        public async Task<CodeReviewComplianceCheck> VerifyComplianceAsync(List<string> requiredFiles)
        {
            var reviewedFiles = await _context.CodeReviews
                .Where(r => r.Status == CodeReviewStatus.Approved)
                .Select(r => r.FilePath)
                .Distinct()
                .ToListAsync();

            var check = new CodeReviewComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalRequiredFiles = requiredFiles.Count,
                ReviewedFiles = reviewedFiles.Count,
                UnreviewedFiles = requiredFiles.Except(reviewedFiles).ToList()
            };

            check.IsCompliant = check.UnreviewedFiles.Count == 0;

            if (!check.IsCompliant)
            {
                check.Issues.Add($"{check.UnreviewedFiles.Count} files have not been reviewed");
                foreach (var file in check.UnreviewedFiles)
                {
                    check.Issues.Add($"File not reviewed: {file}");
                }
            }

            return check;
        }
    }

    // Data Models
    public class CodeReview
    {
        public Guid Id { get; set; }
        public string ReviewNumber { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public int LineStart { get; set; }
        public int LineEnd { get; set; }
        public CodeReviewStatus Status { get; set; }
        public string Author { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<CodeReviewAssignment> Assignments { get; set; } = new();
        public List<ReviewFinding> Findings { get; set; } = new();
    }

    public class CodeReviewAssignment
    {
        public Guid Id { get; set; }
        public Guid ReviewId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public bool IsCertified { get; set; }
        public ReviewAssignmentStatus Status { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ReviewFinding
    {
        public Guid Id { get; set; }
        public Guid ReviewId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public FindingSeverity Severity { get; set; }
        public FindingCategory Category { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Recommendation { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Resolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public enum CodeReviewStatus
    {
        Pending,
        InProgress,
        Completed,
        Approved,
        Rejected,
        NeedsRework
    }

    public enum ReviewAssignmentStatus
    {
        Assigned,
        InProgress,
        Completed
    }

    public enum FindingSeverity
    {
        Critical,   // Must be fixed before approval
        Major,      // Should be fixed
        Minor,      // Nice to fix
        Info        // Informational
    }

    public enum FindingCategory
    {
        Safety,
        Correctness,
        Performance,
        Maintainability,
        Standards,
        Documentation
    }

    public class CodeReviewSummary
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalReviews { get; set; }
        public int PendingReviews { get; set; }
        public int InProgress { get; set; }
        public int Completed { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int TotalFindings { get; set; }
        public int CriticalFindings { get; set; }
        public int MajorFindings { get; set; }
        public int MinorFindings { get; set; }
    }

    public class CodeReviewComplianceCheck
    {
        public DateTime CheckedAt { get; set; }
        public int TotalRequiredFiles { get; set; }
        public int ReviewedFiles { get; set; }
        public List<string> UnreviewedFiles { get; set; } = new();
        public bool IsCompliant { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    // DbContext
    public class CodeReviewDbContext : DbContext
    {
        public CodeReviewDbContext(DbContextOptions<CodeReviewDbContext> options) : base(options) { }

        public DbSet<CodeReview> CodeReviews { get; set; }
        public DbSet<CodeReviewAssignment> CodeReviewAssignments { get; set; }
        public DbSet<ReviewFinding> ReviewFindings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CodeReview>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReviewNumber).IsUnique();
                entity.HasMany(e => e.Assignments).WithOne().HasForeignKey("ReviewId");
                entity.HasMany(e => e.Findings).WithOne().HasForeignKey("ReviewId");
            });
        }
    }
}
