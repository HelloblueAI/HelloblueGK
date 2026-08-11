using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HB_NLP_Research_Lab.Core;

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
            review.Id = Guid.NewGuid();
            review.CreatedAt = DateTime.UtcNow;
            review.Status = CodeReviewStatus.Pending;

            // Allocate via max-suffix + retry so concurrent creates cannot collide on the unique ReviewNumber index.
            const int maxAttempts = 8;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                review.ReviewNumber = await AllocateNextReviewNumberAsync();
                _context.CodeReviews.Add(review);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "Created code review {ReviewNumber} for {FilePath}",
                        review.ReviewNumber,
                        LogSanitizer.Sanitize(review.FilePath));
                    return review;
                }
                catch (DbUpdateException) when (attempt < maxAttempts - 1)
                {
                    _context.Entry(review).State = EntityState.Detached;
                }
            }

            throw new InvalidOperationException("Unable to allocate a unique code review number");
        }

        private async Task<string> AllocateNextReviewNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"CR-{year}-";
            var existing = await _context.CodeReviews
                .Where(cr => cr.ReviewNumber.StartsWith(prefix))
                .Select(cr => cr.ReviewNumber)
                .ToListAsync();

            // Numeric max — not lexicographic OrderByDescending (CR-…-10000 < CR-…-9999 as strings).
            var next = 1;
            foreach (var number in existing)
            {
                if (number.Length > prefix.Length
                    && int.TryParse(number.AsSpan(prefix.Length), out var parsed)
                    && parsed >= next)
                {
                    next = parsed + 1;
                }
            }

            return $"{prefix}{next:D4}";
        }

        /// <summary>
        /// Register (or re-activate) a server-owned certified reviewer.
        /// Certification status is never accepted from assign-reviewer clients.
        /// </summary>
        public async Task<CertifiedReviewer> RegisterCertifiedReviewerAsync(
            string reviewerName,
            string? certifiedBy = null)
        {
            if (string.IsNullOrWhiteSpace(reviewerName))
                throw new ArgumentException("Reviewer name is required", nameof(reviewerName));

            var normalized = NormalizeReviewerName(reviewerName);
            var existing = await FindCertifiedReviewerAsync(normalized);
            if (existing != null)
            {
                existing.IsActive = true;
                existing.ReviewerName = normalized;
                existing.CertifiedBy = string.IsNullOrWhiteSpace(certifiedBy)
                    ? existing.CertifiedBy
                    : certifiedBy.Trim();
                existing.CertifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return existing;
            }

            var reviewer = new CertifiedReviewer
            {
                Id = Guid.NewGuid(),
                ReviewerName = normalized,
                IsActive = true,
                CertifiedBy = string.IsNullOrWhiteSpace(certifiedBy) ? null : certifiedBy.Trim(),
                CertifiedAt = DateTime.UtcNow
            };
            _context.CertifiedReviewers.Add(reviewer);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Registered certified reviewer {ReviewerName}",
                LogSanitizer.SanitizeIdentifier(normalized));
            return reviewer;
        }

        /// <summary>
        /// Revoke a certified reviewer so they can no longer satisfy Level A assignment gates.
        /// </summary>
        public async Task RevokeCertifiedReviewerAsync(string reviewerName)
        {
            if (string.IsNullOrWhiteSpace(reviewerName))
                throw new ArgumentException("Reviewer name is required", nameof(reviewerName));

            var existing = await FindCertifiedReviewerAsync(NormalizeReviewerName(reviewerName));
            if (existing == null)
                throw new ArgumentException($"Certified reviewer '{reviewerName.Trim()}' not found", nameof(reviewerName));

            existing.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Revoked certified reviewer {ReviewerName}",
                LogSanitizer.SanitizeIdentifier(existing.ReviewerName));
        }

        /// <summary>
        /// Assign a server-certified reviewer to a code review.
        /// Client-asserted certification flags are intentionally not accepted.
        /// </summary>
        public async Task AssignReviewerAsync(Guid reviewId, string reviewerName)
        {
            if (string.IsNullOrWhiteSpace(reviewerName))
                throw new ArgumentException("Reviewer name is required", nameof(reviewerName));

            var normalized = NormalizeReviewerName(reviewerName);
            var rosterEntry = await FindCertifiedReviewerAsync(normalized);
            if (rosterEntry is not { IsActive: true })
            {
                throw new InvalidOperationException(
                    "Reviewer must be on the server certified-reviewer roster for Level A reviews");
            }

            var review = await _context.CodeReviews
                .FirstOrDefaultAsync(r => r.Id == reviewId);
            if (review == null)
                throw new ArgumentException($"Review {reviewId} not found");

            var assignment = new CodeReviewAssignment
            {
                Id = Guid.NewGuid(),
                ReviewId = reviewId,
                ReviewerName = rosterEntry.ReviewerName,
                IsCertified = true,
                AssignedAt = DateTime.UtcNow,
                Status = ReviewAssignmentStatus.Assigned
            };

            _context.CodeReviewAssignments.Add(assignment);
            if (review.Status == CodeReviewStatus.Pending)
            {
                review.Status = CodeReviewStatus.InProgress;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Assigned certified reviewer {ReviewerName} to review {ReviewId}",
                LogSanitizer.SanitizeIdentifier(rosterEntry.ReviewerName),
                reviewId);
        }

        private async Task<CertifiedReviewer?> FindCertifiedReviewerAsync(string normalizedReviewerName)
        {
            var candidates = await _context.CertifiedReviewers
                .Where(r => r.ReviewerName == normalizedReviewerName)
                .ToListAsync();

            // Prefer exact Ordinal match after normalize; fall back to case-insensitive for legacy rows.
            return candidates.FirstOrDefault(r =>
                       string.Equals(r.ReviewerName, normalizedReviewerName, StringComparison.Ordinal))
                   ?? candidates.FirstOrDefault(r =>
                       string.Equals(r.ReviewerName, normalizedReviewerName, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeReviewerName(string reviewerName) => reviewerName.Trim();

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

            // Terminal statuses must not accept new findings — otherwise a late submit
            // can clobber Approved → Completed and reopen a compliance forge window.
            if (review.Status is CodeReviewStatus.Approved or CodeReviewStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot submit findings for review with status {review.Status}");
            }

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
                .Include(r => r.Assignments)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                throw new ArgumentException($"Review {reviewId} not found");

            if (review.Status is CodeReviewStatus.Approved or CodeReviewStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot approve review with status {review.Status}");
            }

            // Level A reviews require at least one completed certified reviewer assignment.
            // Without this gate, create+approve forges compliance while bypassing assign/findings.
            var hasCompletedCertifiedReviewer = review.Assignments.Any(a =>
                a.IsCertified && a.Status == ReviewAssignmentStatus.Completed);
            if (!hasCompletedCertifiedReviewer)
            {
                throw new InvalidOperationException(
                    "Cannot approve review without at least one completed certified reviewer assignment");
            }

            var allAssignmentsCompleted = review.Assignments.Count > 0 &&
                review.Assignments.All(a => a.Status == ReviewAssignmentStatus.Completed);
            if (!allAssignmentsCompleted)
            {
                throw new InvalidOperationException(
                    "Cannot approve review until all assigned reviewers have completed their findings");
            }

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
            requiredFiles ??= new List<string>();
            var normalizedRequired = requiredFiles
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(NormalizeFilePath)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var approvedFiles = (await _context.CodeReviews
                .Where(r => r.Status == CodeReviewStatus.Approved)
                .Select(r => r.FilePath)
                .Distinct()
                .ToListAsync())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(NormalizeFilePath)
                .ToHashSet(StringComparer.Ordinal);

            var check = new CodeReviewComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalRequiredFiles = normalizedRequired.Count,
                // Count only required files that have an approved review — not all approved rows.
                ReviewedFiles = normalizedRequired.Count(approvedFiles.Contains),
                UnreviewedFiles = normalizedRequired.Where(f => !approvedFiles.Contains(f)).ToList()
            };

            // Fail closed: an empty required set must never imply Level A compliance.
            if (normalizedRequired.Count == 0)
            {
                check.IsCompliant = false;
                check.Issues.Add("Required file list is empty; code-review compliance cannot be asserted");
                return check;
            }

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

        private static string NormalizeFilePath(string filePath) =>
            filePath.Trim().Replace('\\', '/');
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

    /// <summary>
    /// Server-owned certified reviewer roster. Assignment IsCertified is derived from this store.
    /// </summary>
    public class CertifiedReviewer
    {
        public Guid Id { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? CertifiedBy { get; set; }
        public DateTime CertifiedAt { get; set; }
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
        public DbSet<CertifiedReviewer> CertifiedReviewers { get; set; }

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

            modelBuilder.Entity<CertifiedReviewer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReviewerName).IsUnique();
                entity.Property(e => e.ReviewerName).IsRequired().HasMaxLength(256);
            });
        }
    }
}
