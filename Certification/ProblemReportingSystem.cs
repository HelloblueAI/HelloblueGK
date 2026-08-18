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
    /// Problem Reporting System for DO-178C Level A / NASA NPR 7150.2 Class A
    /// Tracks all problems, anomalies, and issues throughout the software lifecycle
    /// </summary>
    public class ProblemReportingSystem
    {
        private readonly ProblemReportDbContext _context;
        private readonly ILogger<ProblemReportingSystem> _logger;

        public ProblemReportingSystem(ProblemReportDbContext context, ILogger<ProblemReportingSystem> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new problem report (PR)
        /// </summary>
        /// <param name="report">Problem report payload</param>
        /// <param name="explicitSeverity">
        /// Optional admin-asserted severity. Keyword classification from Impact is applied as a floor
        /// so free-text wording cannot under-classify safety/critical impact as Minor.
        /// </param>
        public async Task<ProblemReport> CreateProblemReportAsync(
            ProblemReport report,
            ProblemSeverity? explicitSeverity = null)
        {
            report.Id = Guid.NewGuid();
            report.CreatedAt = DateTime.UtcNow;
            report.Status = ProblemReportStatus.Open;
            report.Severity = ResolveSeverity(report.Impact, explicitSeverity);

            const int maxAttempts = 8;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                report.ReportNumber = await AllocateNextReportNumberAsync();
                _context.ProblemReports.Add(report);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogWarning(
                        "Created problem report {ReportNumber}: {Title}",
                        report.ReportNumber,
                        LogSanitizer.Sanitize(report.Title));
                    return report;
                }
                catch (DbUpdateException) when (attempt < maxAttempts - 1)
                {
                    // Unique ReportNumber race — detach and retry with a fresh sequence value.
                    _context.Entry(report).State = EntityState.Detached;
                }
            }

            throw new InvalidOperationException("Unable to allocate a unique problem report number");
        }

        /// <summary>
        /// Update problem report status
        /// </summary>
        public async Task UpdateStatusAsync(
            string reportNumber,
            ProblemReportStatus newStatus,
            string? resolution = null,
            string? changedBy = null)
        {
            var report = await _context.ProblemReports
                .Include(pr => pr.RequirementLinks)
                .Include(pr => pr.TestLinks)
                .FirstOrDefaultAsync(pr => pr.ReportNumber == reportNumber);

            if (report == null)
                throw new ArgumentException($"Problem report {reportNumber} not found");

            var oldStatus = report.Status;
            if (oldStatus == newStatus)
                throw new InvalidOperationException($"Problem report {reportNumber} is already in status {newStatus}");

            if (!IsAllowedStatusTransition(oldStatus, newStatus))
            {
                throw new InvalidOperationException(
                    $"Problem report {reportNumber} cannot transition from {oldStatus} to {newStatus}");
            }

            if (newStatus == ProblemReportStatus.Closed)
            {
                if (string.IsNullOrWhiteSpace(resolution))
                {
                    throw new InvalidOperationException(
                        $"Problem report {reportNumber} requires a non-empty resolution before closing");
                }

                var normalizedResolution = resolution.Trim();
                if (!HasSubstantiveResolution(normalizedResolution))
                {
                    throw new InvalidOperationException(
                        $"Problem report {reportNumber} requires a substantive resolution (not vacuous text such as 'done'/'fixed')");
                }

                // Critical/Major closures need linked evidence — resolution text alone forges IsCompliant.
                if (report.Severity is ProblemSeverity.Critical or ProblemSeverity.Major &&
                    !HasResolutionEvidence(report))
                {
                    throw new InvalidOperationException(
                        $"Problem report {reportNumber} requires a linked requirement or test case before closing");
                }

                report.Resolution = normalizedResolution;
                report.ClosedAt = DateTime.UtcNow;
            }

            report.Status = newStatus;
            report.UpdatedAt = DateTime.UtcNow;

            // Track status changes — capture OldStatus before mutation for an accurate audit trail.
            var statusChange = new ProblemReportStatusChange
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "System" : changedBy.Trim(),
                Reason = resolution
            };

            _context.ProblemReportStatusChanges.Add(statusChange);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated problem report {ReportNumber} status to {Status}", LogSanitizer.SanitizeIdentifier(reportNumber), newStatus);
        }

        /// <summary>
        /// Link problem report to requirement
        /// </summary>
        public async Task LinkToRequirementAsync(string reportNumber, Guid requirementId)
        {
            if (requirementId == Guid.Empty)
                throw new ArgumentException("RequirementId must be a non-empty GUID", nameof(requirementId));

            var report = await _context.ProblemReports
                .FirstOrDefaultAsync(pr => pr.ReportNumber == reportNumber);

            if (report == null)
                throw new ArgumentException($"Problem report {reportNumber} not found");

            var link = new ProblemReportRequirementLink
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                RequirementId = requirementId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProblemReportRequirementLinks.Add(link);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked problem report {ReportNumber} to requirement {RequirementId}", 
                reportNumber, requirementId);
        }

        /// <summary>
        /// Link problem report to test case
        /// </summary>
        public async Task LinkToTestAsync(string reportNumber, string testCaseId)
        {
            if (string.IsNullOrWhiteSpace(testCaseId))
                throw new ArgumentException("TestCaseId must be a non-empty identifier", nameof(testCaseId));

            var report = await _context.ProblemReports
                .FirstOrDefaultAsync(pr => pr.ReportNumber == reportNumber);

            if (report == null)
                throw new ArgumentException($"Problem report {reportNumber} not found");

            var link = new ProblemReportTestLink
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                TestCaseId = testCaseId.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ProblemReportTestLinks.Add(link);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Generate problem report summary for certification
        /// </summary>
        public async Task<ProblemReportSummary> GenerateSummaryAsync()
        {
            var reports = await _context.ProblemReports
                .Include(pr => pr.StatusChanges)
                .ToListAsync();

            var summary = new ProblemReportSummary
            {
                GeneratedAt = DateTime.UtcNow,
                TotalReports = reports.Count,
                OpenReports = reports.Count(r => r.Status == ProblemReportStatus.Open),
                UnderInvestigation = reports.Count(r => r.Status == ProblemReportStatus.UnderInvestigation),
                Resolved = reports.Count(r => r.Status == ProblemReportStatus.Resolved),
                Closed = reports.Count(r => r.Status == ProblemReportStatus.Closed),
                CriticalSeverity = reports.Count(r => r.Severity == ProblemSeverity.Critical),
                MajorSeverity = reports.Count(r => r.Severity == ProblemSeverity.Major),
                MinorSeverity = reports.Count(r => r.Severity == ProblemSeverity.Minor),
                AverageResolutionTime = CalculateAverageResolutionTime(reports),
                Reports = reports.Select(r => new ProblemReportSummaryEntry
                {
                    ReportNumber = r.ReportNumber,
                    Title = r.Title,
                    Status = r.Status,
                    Severity = r.Severity,
                    CreatedAt = r.CreatedAt,
                    ClosedAt = r.ClosedAt,
                    ResolutionTime = r.ClosedAt.HasValue 
                        ? (r.ClosedAt.Value - r.CreatedAt).TotalDays 
                        : (double?)null
                }).ToList()
            };

            return summary;
        }

        /// <summary>
        /// Verify all critical problems are resolved before certification
        /// </summary>
        public async Task<ProblemReportComplianceCheck> VerifyComplianceAsync()
        {
            var totalReports = await _context.ProblemReports.CountAsync();
            var reports = await _context.ProblemReports
                .Include(pr => pr.RequirementLinks)
                .Include(pr => pr.TestLinks)
                .Where(pr => pr.Severity == ProblemSeverity.Critical || 
                            pr.Severity == ProblemSeverity.Major)
                .ToListAsync();

            var check = new ProblemReportComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalCriticalProblems = reports.Count(r => r.Severity == ProblemSeverity.Critical),
                UnresolvedCriticalProblems = reports.Count(r => 
                    r.Severity == ProblemSeverity.Critical && 
                    r.Status != ProblemReportStatus.Closed),
                TotalMajorProblems = reports.Count(r => r.Severity == ProblemSeverity.Major),
                UnresolvedMajorProblems = reports.Count(r => 
                    r.Severity == ProblemSeverity.Major && 
                    r.Status != ProblemReportStatus.Closed)
            };

            // Empty problem-report store must fail closed — 0 unresolved on 0 reports is not evidence.
            if (totalReports == 0)
            {
                check.IsCompliant = false;
                check.Issues.Add("No problem reports recorded; DO-178C Level A problem-reporting compliance cannot be asserted");
                return check;
            }

            // Closed without substantive resolution + evidence links must not satisfy certification gates.
            var improperlyClosed = reports.Count(r =>
                r.Status == ProblemReportStatus.Closed &&
                (string.IsNullOrWhiteSpace(r.Resolution) ||
                 !HasSubstantiveResolution(r.Resolution) ||
                 !HasResolutionEvidence(r)));
            if (improperlyClosed > 0)
            {
                check.Issues.Add(
                    $"{improperlyClosed} critical/major problem report(s) are Closed without substantive resolution evidence");
            }

            var unresolvedOk = check.UnresolvedCriticalProblems == 0 &&
                               check.UnresolvedMajorProblems == 0;
            if (!unresolvedOk)
            {
                if (check.UnresolvedCriticalProblems > 0)
                    check.Issues.Add("Critical problems must be resolved before certification");
                if (check.UnresolvedMajorProblems > 0)
                    check.Issues.Add("Major problems must be resolved before certification");
            }

            check.IsCompliant = unresolvedOk && improperlyClosed == 0;
            return check;
        }

        private static bool HasResolutionEvidence(ProblemReport report) =>
            report.RequirementLinks.Count > 0 || report.TestLinks.Count > 0;

        /// <summary>
        /// Reject vacuous closure text ("done", "fixed", "ok") that previously forged IsCompliant.
        /// </summary>
        internal static bool HasSubstantiveResolution(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution))
                return false;

            var trimmed = resolution.Trim();
            if (trimmed.Length < 12)
                return false;

            var normalized = trimmed.ToLowerInvariant();
            return normalized is not (
                "done" or "fixed" or "ok" or "okay" or "closed" or "resolved" or
                "n/a" or "na" or "none" or "complete" or "completed" or "pass" or "passed");
        }

        private static bool IsAllowedStatusTransition(ProblemReportStatus from, ProblemReportStatus to)
        {
            return (from, to) switch
            {
                (ProblemReportStatus.Open, ProblemReportStatus.UnderInvestigation) => true,
                (ProblemReportStatus.Open, ProblemReportStatus.Rejected) => true,
                (ProblemReportStatus.UnderInvestigation, ProblemReportStatus.Resolved) => true,
                (ProblemReportStatus.UnderInvestigation, ProblemReportStatus.Rejected) => true,
                (ProblemReportStatus.UnderInvestigation, ProblemReportStatus.Open) => true,
                // Closure requires an investigated/resolved path — Open→Closed forges compliance.
                (ProblemReportStatus.Resolved, ProblemReportStatus.Closed) => true,
                (ProblemReportStatus.Resolved, ProblemReportStatus.UnderInvestigation) => true,
                (ProblemReportStatus.Closed, ProblemReportStatus.Open) => true,
                (ProblemReportStatus.Rejected, ProblemReportStatus.Open) => true,
                _ => false
            };
        }

        private async Task<string> AllocateNextReportNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"PR-{year}-";
            var existing = await _context.ProblemReports
                .Where(pr => pr.ReportNumber.StartsWith(prefix))
                .Select(pr => pr.ReportNumber)
                .ToListAsync();

            // Numeric max — not lexicographic OrderByDescending (PR-…-10000 < PR-…-9999 as strings).
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
        /// Resolve severity with fail-closed defaults and a keyword floor.
        /// Unclassified impact defaults to Critical so Level A compliance cannot be forged by omitting
        /// "critical/safety/major" keywords. Explicit severity may not under-classify below the floor.
        /// </summary>
        public static ProblemSeverity ResolveSeverity(string? impact, ProblemSeverity? explicitSeverity)
        {
            var keywordFloor = ClassifyImpactKeywords(impact);
            var resolved = explicitSeverity ?? keywordFloor ?? ProblemSeverity.Critical;

            if (keywordFloor.HasValue && (int)resolved > (int)keywordFloor.Value)
            {
                // Enum order is Critical(0) < Major(1) < Minor(2); higher int = lower severity.
                resolved = keywordFloor.Value;
            }

            return resolved;
        }

        private static ProblemSeverity? ClassifyImpactKeywords(string? impact)
        {
            if (string.IsNullOrWhiteSpace(impact))
                return null;

            if (impact.Contains("safety", StringComparison.OrdinalIgnoreCase) ||
                impact.Contains("critical", StringComparison.OrdinalIgnoreCase))
                return ProblemSeverity.Critical;

            if (impact.Contains("major", StringComparison.OrdinalIgnoreCase) ||
                impact.Contains("significant", StringComparison.OrdinalIgnoreCase))
                return ProblemSeverity.Major;

            return null;
        }

        private double CalculateAverageResolutionTime(List<ProblemReport> reports)
        {
            var resolvedReports = reports
                .Where(r => r.ClosedAt.HasValue)
                .ToList();

            if (!resolvedReports.Any())
                return 0;

            var totalDays = resolvedReports
                .Sum(r => (r.ClosedAt!.Value - r.CreatedAt).TotalDays);

            return totalDays / resolvedReports.Count;
        }
    }

    // Data Models
    public class ProblemReport
    {
        public Guid Id { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public ProblemSeverity Severity { get; set; }
        public ProblemReportStatus Status { get; set; }
        public string ReportedBy { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public string? Resolution { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        // Navigation properties
        public List<ProblemReportStatusChange> StatusChanges { get; set; } = new();
        public List<ProblemReportRequirementLink> RequirementLinks { get; set; } = new();
        public List<ProblemReportTestLink> TestLinks { get; set; } = new();
    }

    public class ProblemReportStatusChange
    {
        public Guid Id { get; set; }
        public Guid ProblemReportId { get; set; }
        public ProblemReportStatus OldStatus { get; set; }
        public ProblemReportStatus NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class ProblemReportRequirementLink
    {
        public Guid Id { get; set; }
        public Guid ProblemReportId { get; set; }
        public Guid RequirementId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProblemReportTestLink
    {
        public Guid Id { get; set; }
        public Guid ProblemReportId { get; set; }
        public string TestCaseId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public enum ProblemSeverity
    {
        Critical,   // Safety-critical, blocks certification
        Major,      // Significant impact, must be fixed
        Minor       // Low impact, should be fixed
    }

    public enum ProblemReportStatus
    {
        Open,
        UnderInvestigation,
        Resolved,
        Closed,
        Rejected
    }

    public class ProblemReportSummary
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalReports { get; set; }
        public int OpenReports { get; set; }
        public int UnderInvestigation { get; set; }
        public int Resolved { get; set; }
        public int Closed { get; set; }
        public int CriticalSeverity { get; set; }
        public int MajorSeverity { get; set; }
        public int MinorSeverity { get; set; }
        public double AverageResolutionTime { get; set; }
        public List<ProblemReportSummaryEntry> Reports { get; set; } = new();
    }

    public class ProblemReportSummaryEntry
    {
        public string ReportNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ProblemReportStatus Status { get; set; }
        public ProblemSeverity Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public double? ResolutionTime { get; set; }
    }

    public class ProblemReportComplianceCheck
    {
        public DateTime CheckedAt { get; set; }
        public int TotalCriticalProblems { get; set; }
        public int UnresolvedCriticalProblems { get; set; }
        public int TotalMajorProblems { get; set; }
        public int UnresolvedMajorProblems { get; set; }
        public bool IsCompliant { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    // DbContext for Problem Reports
    public class ProblemReportDbContext : DbContext
    {
        public ProblemReportDbContext(DbContextOptions<ProblemReportDbContext> options) : base(options) { }

        public DbSet<ProblemReport> ProblemReports { get; set; }
        public DbSet<ProblemReportStatusChange> ProblemReportStatusChanges { get; set; }
        public DbSet<ProblemReportRequirementLink> ProblemReportRequirementLinks { get; set; }
        public DbSet<ProblemReportTestLink> ProblemReportTestLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProblemReport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ReportNumber).IsUnique();
                entity.HasMany(e => e.StatusChanges).WithOne().HasForeignKey("ProblemReportId");
            });
        }
    }
}
