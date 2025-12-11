using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        public async Task<ProblemReport> CreateProblemReportAsync(ProblemReport report)
        {
            // Generate PR number (format: PR-YYYY-NNNN)
            var year = DateTime.UtcNow.Year;
            var existingCount = await _context.ProblemReports
                .CountAsync(pr => pr.ReportNumber.StartsWith($"PR-{year}-"));
            
            report.ReportNumber = $"PR-{year}-{(existingCount + 1):D4}";
            report.Id = Guid.NewGuid();
            report.CreatedAt = DateTime.UtcNow;
            report.Status = ProblemReportStatus.Open;
            report.Severity = DetermineSeverity(report);

            _context.ProblemReports.Add(report);
            await _context.SaveChangesAsync();

            _logger.LogWarning("Created problem report {ReportNumber}: {Title}", report.ReportNumber, report.Title);
            return report;
        }

        /// <summary>
        /// Update problem report status
        /// </summary>
        public async Task UpdateStatusAsync(string reportNumber, ProblemReportStatus newStatus, string? resolution = null)
        {
            var report = await _context.ProblemReports
                .FirstOrDefaultAsync(pr => pr.ReportNumber == reportNumber);

            if (report == null)
                throw new ArgumentException($"Problem report {reportNumber} not found");

            report.Status = newStatus;
            report.UpdatedAt = DateTime.UtcNow;

            if (newStatus == ProblemReportStatus.Closed && !string.IsNullOrEmpty(resolution))
            {
                report.Resolution = resolution;
                report.ClosedAt = DateTime.UtcNow;
            }

            // Track status changes
            var statusChange = new ProblemReportStatusChange
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                OldStatus = report.Status,
                NewStatus = newStatus,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = "System", // Should be actual user
                Reason = resolution
            };

            _context.ProblemReportStatusChanges.Add(statusChange);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated problem report {ReportNumber} status to {Status}", reportNumber, newStatus);
        }

        /// <summary>
        /// Link problem report to requirement
        /// </summary>
        public async Task LinkToRequirementAsync(string reportNumber, Guid requirementId)
        {
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
            var report = await _context.ProblemReports
                .FirstOrDefaultAsync(pr => pr.ReportNumber == reportNumber);

            if (report == null)
                throw new ArgumentException($"Problem report {reportNumber} not found");

            var link = new ProblemReportTestLink
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                TestCaseId = testCaseId,
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
            var reports = await _context.ProblemReports
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

            check.IsCompliant = check.UnresolvedCriticalProblems == 0 && 
                               check.UnresolvedMajorProblems == 0;

            if (!check.IsCompliant)
            {
                check.Issues.Add("Critical problems must be resolved before certification");
                check.Issues.Add("Major problems must be resolved before certification");
            }

            return check;
        }

        private ProblemSeverity DetermineSeverity(ProblemReport report)
        {
            // Determine severity based on impact
            if (report.Impact.Contains("safety", StringComparison.OrdinalIgnoreCase) ||
                report.Impact.Contains("critical", StringComparison.OrdinalIgnoreCase))
                return ProblemSeverity.Critical;

            if (report.Impact.Contains("major", StringComparison.OrdinalIgnoreCase) ||
                report.Impact.Contains("significant", StringComparison.OrdinalIgnoreCase))
                return ProblemSeverity.Major;

            return ProblemSeverity.Minor;
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
