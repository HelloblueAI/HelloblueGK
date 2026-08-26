using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            ArgumentNullException.ThrowIfNull(review);

            review.FilePath = NormalizeReviewFilePath(review.FilePath);
            review.FunctionName = NormalizeRequiredText(review.FunctionName, "Function name");
            if (review.LineStart <= 0 || review.LineEnd < review.LineStart)
            {
                throw new ArgumentException(
                    "Code review line range must be a positive, ordered span",
                    nameof(review));
            }

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

            if (review.Status is CodeReviewStatus.Approved or CodeReviewStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot assign a reviewer to a review with status {review.Status}");
            }

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
            ArgumentNullException.ThrowIfNull(findings);
            if (findings.Count == 0)
            {
                throw new ArgumentException(
                    "At least one review finding is required; an empty findings list cannot complete a certified assignment",
                    nameof(findings));
            }

            foreach (var finding in findings)
            {
                if (finding.LineNumber <= 0)
                {
                    throw new ArgumentException(
                        "Finding line number must be a positive line in the reviewed span",
                        nameof(findings));
                }

                if (string.IsNullOrWhiteSpace(finding.Description))
                {
                    throw new ArgumentException("Finding description is required", nameof(findings));
                }

                finding.Description = finding.Description.Trim();
            }

            var review = await _context.CodeReviews
                .Include(r => r.Assignments)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                throw new ArgumentException($"Review {reviewId} not found");

            // Terminal statuses must not accept new findings — otherwise a late submit
            // can clobber Approved → Completed, or inject findings into a Completed
            // snapshot before approve, and reopen a compliance forge window.
            if (review.Status is CodeReviewStatus.Approved
                or CodeReviewStatus.Rejected
                or CodeReviewStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Cannot submit findings for review with status {review.Status}");
            }

            var assignment = review.Assignments.FirstOrDefault(a => a.ReviewerName == reviewerName);
            if (assignment == null)
                throw new ArgumentException($"Reviewer {reviewerName} not assigned to review {reviewId}");

            foreach (var finding in findings)
            {
                if (string.IsNullOrWhiteSpace(finding.Description))
                    throw new ArgumentException("Finding description is required", nameof(findings));

                finding.Id = Guid.NewGuid();
                finding.ReviewId = reviewId;
                finding.ReviewerName = reviewerName;
                finding.Description = finding.Description.Trim();
                finding.CreatedAt = DateTime.UtcNow;
                finding.Severity = ResolveFindingSeverity(finding);

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
        /// Resolve finding severity with Safety elevation and a description keyword floor.
        /// Explicit Minor/Info cannot hide Critical/Major language from approval gates.
        /// Unclassified (no Safety category, no keywords) keeps the client severity.
        /// </summary>
        public static FindingSeverity ResolveFindingSeverity(ReviewFinding finding)
        {
            ArgumentNullException.ThrowIfNull(finding);

            var resolved = finding.Severity;
            if (finding.Category == FindingCategory.Safety)
            {
                resolved = FindingSeverity.Critical;
            }

            var keywordFloor = ClassifyFindingKeywords(finding.Description, finding.Recommendation);
            if (keywordFloor.HasValue && (int)resolved > (int)keywordFloor.Value)
            {
                // Enum order is Critical(0) < Major(1) < Minor(2) < Info(3); higher int = lower severity.
                resolved = keywordFloor.Value;
            }

            return resolved;
        }

        // Whole-token stems plus explicit inflections: "hazards"/"critically" still elevate,
        // but "insignificant" must not hit "significant" and "non-critical" must not hit
        // "critical". Hyphenated compounds like "safety-critical" still match.
        private static readonly Regex CriticalKeywordPattern = new(
            @"(?<!non[- ]?)(?<![A-Za-z0-9])(safety|safeties|critical(?:ly|ity)?|catastrophic(?:ally)?|hazard(?:s|ous|ously)?)(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex MajorKeywordPattern = new(
            @"(?<!non[- ]?)(?<![A-Za-z0-9])(major(?:ly)?|significant(?:ly)?|significance)(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static FindingSeverity? ClassifyFindingKeywords(string? description, string? recommendation)
        {
            var text = $"{description} {recommendation}";
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (CriticalKeywordPattern.IsMatch(text))
                return FindingSeverity.Critical;

            if (MajorKeywordPattern.IsMatch(text))
                return FindingSeverity.Major;

            return null;
        }

        /// <summary>
        /// Disposition (resolve) a finding so Major/Critical no longer block approval.
        /// </summary>
        public async Task ResolveFindingAsync(Guid reviewId, Guid findingId, string resolvedBy)
        {
            if (string.IsNullOrWhiteSpace(resolvedBy))
                throw new ArgumentException("Resolver is required", nameof(resolvedBy));

            var finding = await _context.ReviewFindings
                .FirstOrDefaultAsync(f => f.Id == findingId && f.ReviewId == reviewId);
            if (finding == null)
                throw new ArgumentException($"Finding {findingId} not found for review {reviewId}");

            finding.Resolved = true;
            finding.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Resolved finding {FindingId} on review {ReviewId} by {ResolvedBy}",
                findingId,
                reviewId,
                LogSanitizer.SanitizeIdentifier(resolvedBy));
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

            if (review.Status != CodeReviewStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Cannot approve review with status {review.Status}; review must be Completed");
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

            // Level A independence: approver must not be the author or a completing reviewer.
            var normalizedApprover = NormalizeReviewerName(approvedBy);
            if (string.IsNullOrWhiteSpace(normalizedApprover))
            {
                throw new ArgumentException("Approver is required", nameof(approvedBy));
            }

            if (!string.IsNullOrWhiteSpace(review.Author) &&
                string.Equals(NormalizeReviewerName(review.Author), normalizedApprover, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cannot approve a code review as its author; Level A requires an independent approver");
            }

            var completingReviewers = review.Assignments
                .Where(a => a.Status == ReviewAssignmentStatus.Completed)
                .Select(a => a.ReviewerName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            if (completingReviewers.Any(name =>
                    string.Equals(NormalizeReviewerName(name), normalizedApprover, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Cannot approve a code review as one of its completing reviewers; Level A requires separation of duties");
            }

            // Critical and Major findings block approval until dispositioned (Resolved).
            // SubmitFindingsAsync applies Safety elevation + description keyword floors so
            // clients cannot under-classify blocking findings to Minor/Info.
            var blockingFindings = review.Findings
                .Where(f => !f.Resolved &&
                            (f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major))
                .ToList();
            if (blockingFindings.Count > 0)
            {
                var criticalCount = blockingFindings.Count(f => f.Severity == FindingSeverity.Critical);
                var majorCount = blockingFindings.Count(f => f.Severity == FindingSeverity.Major);
                throw new InvalidOperationException(
                    $"Cannot approve review with {criticalCount} unresolved critical findings and {majorCount} unresolved major findings");
            }

            // Atomic Completed → Approved claim closes the load/check/SaveChanges TOCTOU where a
            // concurrent SubmitFindings inserts Critical findings and still leaves Approved.
            var approvedAt = DateTime.UtcNow;
            var claimed = await _context.CodeReviews
                .Where(r => r.Id == reviewId && r.Status == CodeReviewStatus.Completed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, CodeReviewStatus.Approved)
                    .SetProperty(r => r.ApprovedBy, normalizedApprover)
                    .SetProperty(r => r.ApprovedAt, approvedAt));

            if (claimed == 0)
            {
                await _context.Entry(review).ReloadAsync();
                throw new InvalidOperationException(
                    $"Cannot approve review with status {review.Status}; concurrent status change detected");
            }

            // Re-check blocking findings after the claim. If a concurrent submit raced in,
            // revert the approval so Approved never coexists with unresolved Critical/Major.
            var blockingAfterClaim = await _context.ReviewFindings
                .CountAsync(f => f.ReviewId == reviewId
                    && !f.Resolved
                    && (f.Severity == FindingSeverity.Critical || f.Severity == FindingSeverity.Major));
            if (blockingAfterClaim > 0)
            {
                await _context.CodeReviews
                    .Where(r => r.Id == reviewId && r.Status == CodeReviewStatus.Approved)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(r => r.Status, CodeReviewStatus.Completed)
                        .SetProperty(r => r.ApprovedBy, (string?)null)
                        .SetProperty(r => r.ApprovedAt, (DateTime?)null));

                throw new InvalidOperationException(
                    $"Cannot approve review with {blockingAfterClaim} unresolved critical/major findings");
            }

            review.Status = CodeReviewStatus.Approved;
            review.ApprovedBy = normalizedApprover;
            review.ApprovedAt = approvedAt;

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
        /// Register (or re-activate) a server-owned required review file.
        /// Compliance scope is never accepted from verify-compliance clients.
        /// </summary>
        public async Task<RequiredReviewFile> RegisterRequiredFileAsync(
            string filePath,
            string? registeredBy = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            var normalized = NormalizeFilePath(filePath);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("File path is required", nameof(filePath));

            var matches = (await _context.RequiredReviewFiles.ToListAsync())
                .Where(f => string.Equals(
                    NormalizeFilePath(f.FilePath),
                    normalized,
                    StringComparison.Ordinal))
                .OrderByDescending(f => f.IsActive)
                .ThenByDescending(f => f.RegisteredAt)
                .ToList();

            var existing = matches.FirstOrDefault();
            if (existing != null)
            {
                existing.IsActive = true;
                existing.FilePath = normalized;
                existing.RegisteredBy = string.IsNullOrWhiteSpace(registeredBy)
                    ? existing.RegisteredBy
                    : registeredBy.Trim();
                existing.RegisteredAt = DateTime.UtcNow;

                // Collapse case-variant duplicates so compliance cannot double-count
                // and unique indexes stay consistent on case-sensitive stores.
                foreach (var duplicate in matches.Skip(1))
                {
                    duplicate.IsActive = false;
                    if (!string.Equals(duplicate.FilePath, normalized, StringComparison.Ordinal))
                    {
                        duplicate.FilePath = $"{normalized}#duplicate-{duplicate.Id:N}";
                    }
                }

                await _context.SaveChangesAsync();
                return existing;
            }

            var required = new RequiredReviewFile
            {
                Id = Guid.NewGuid(),
                FilePath = normalized,
                IsActive = true,
                RegisteredBy = string.IsNullOrWhiteSpace(registeredBy) ? null : registeredBy.Trim(),
                RegisteredAt = DateTime.UtcNow
            };
            _context.RequiredReviewFiles.Add(required);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Registered required review file {FilePath}",
                LogSanitizer.Sanitize(normalized));
            return required;
        }

        /// <summary>
        /// Revoke a required review file so it no longer participates in compliance scope.
        /// </summary>
        public async Task RevokeRequiredFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            var existing = await FindRequiredReviewFileAsync(NormalizeFilePath(filePath));
            if (existing == null)
                throw new ArgumentException($"Required review file '{filePath.Trim()}' not found", nameof(filePath));

            existing.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Revoked required review file {FilePath}",
                LogSanitizer.Sanitize(existing.FilePath));
        }

        /// <summary>
        /// Verify all server-roster required files have approved code reviews.
        /// Client-supplied required-file lists are intentionally not accepted.
        /// </summary>
        public async Task<CodeReviewComplianceCheck> VerifyComplianceAsync()
        {
            var normalizedRequired = (await _context.RequiredReviewFiles
                .Where(f => f.IsActive)
                .Select(f => f.FilePath)
                .ToListAsync())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(NormalizeFilePath)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return await BuildComplianceCheckAsync(normalizedRequired);
        }

        private async Task<CodeReviewComplianceCheck> BuildComplianceCheckAsync(List<string> normalizedRequired)
        {
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
                check.Issues.Add("Required file roster is empty; code-review compliance cannot be asserted");
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

        private async Task<RequiredReviewFile?> FindRequiredReviewFileAsync(string normalizedFilePath)
        {
            // Case-insensitive match so mixed-casing rows collapse onto the canonical path.
            var candidates = await _context.RequiredReviewFiles.ToListAsync();
            return candidates
                .OrderByDescending(f => f.IsActive)
                .ThenByDescending(f => f.RegisteredAt)
                .FirstOrDefault(f =>
                    string.Equals(
                        NormalizeFilePath(f.FilePath),
                        normalizedFilePath,
                        StringComparison.Ordinal));
        }

        /// <summary>
        /// Canonical review path: trim, forward slashes, lowercase — so case-sensitive
        /// stores cannot hold duplicate roster rows for the same logical file.
        /// </summary>
        private static string NormalizeFilePath(string filePath) =>
            filePath.Trim().Replace('\\', '/').ToLowerInvariant();

        private static string NormalizeReviewFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required", nameof(filePath));
            }

            var normalized = NormalizeFilePath(filePath);
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.StartsWith("//", StringComparison.Ordinal)
                || normalized.Contains("://", StringComparison.Ordinal)
                || normalized.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "File path must be relative to the repository.",
                    nameof(filePath));
            }

            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0
                || segments.Any(segment => segment is "." or ".."))
            {
                throw new ArgumentException(
                    "File path must not contain traversal segments.",
                    nameof(filePath));
            }

            return string.Join("/", segments);
        }

        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName} is required", fieldName);
            }

            return value.Trim();
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

    /// <summary>
    /// Server-owned inventory of files that must have approved Level A code reviews.
    /// Compliance scope is derived from this store — never from client request bodies.
    /// </summary>
    public class RequiredReviewFile
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string? RegisteredBy { get; set; }
        public DateTime RegisteredAt { get; set; }
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
        public DbSet<RequiredReviewFile> RequiredReviewFiles { get; set; }

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

            modelBuilder.Entity<RequiredReviewFile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FilePath).IsUnique();
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1024);
            });
        }
    }
}
