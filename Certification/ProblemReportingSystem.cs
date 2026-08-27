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
        private readonly RequirementsDbContext _requirementsContext;
        private readonly TestCoverageDbContext? _coverageContext;
        private readonly ILogger<ProblemReportingSystem> _logger;

        public ProblemReportingSystem(
            ProblemReportDbContext context,
            ILogger<ProblemReportingSystem> logger,
            RequirementsDbContext requirementsContext,
            TestCoverageDbContext? coverageContext = null)
        {
            _context = context;
            _logger = logger;
            _requirementsContext = requirementsContext;
            _coverageContext = coverageContext;
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
            ArgumentNullException.ThrowIfNull(report);
            report.Title = NormalizeRequiredText(report.Title, nameof(report.Title));
            report.Description = NormalizeRequiredText(report.Description, nameof(report.Description));

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

            string? resolutionToPersist = report.Resolution;
            DateTime? closedAtToPersist = report.ClosedAt;
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

                // Critical/Major closures need verified implementation evidence — a
                // Draft/NotTraced shell requirement or invented test id forges IsCompliant.
                if (report.Severity is ProblemSeverity.Critical or ProblemSeverity.Major &&
                    !await HasValidResolutionEvidenceAsync(report))
                {
                    throw new InvalidOperationException(
                        $"Problem report {reportNumber} requires a linked requirement with verified implementation evidence or a recorded test case before closing");
                }

                resolutionToPersist = normalizedResolution;
                closedAtToPersist = DateTime.UtcNow;
            }

            // Atomic expected-status claim closes load/check/SaveChanges TOCTOU
            // (concurrent Open→UnderInvestigation vs Open→Rejected last-writer-wins).
            // Claim + audit insert share one transaction so a failed audit save does not
            // leave a claimed status without a trail. Do not assign Status/etc. and
            // SaveChanges the ProblemReport after the claim — that rewrite can revert a
            // later allowed transition that committed between claim and reload.
            var updatedAt = DateTime.UtcNow;
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var claimed = await _context.ProblemReports
                .Where(pr => pr.Id == report.Id && pr.Status == oldStatus)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(pr => pr.Status, newStatus)
                    .SetProperty(pr => pr.UpdatedAt, updatedAt)
                    .SetProperty(pr => pr.Resolution, resolutionToPersist)
                    .SetProperty(pr => pr.ClosedAt, closedAtToPersist));

            if (claimed == 0)
            {
                await _context.Entry(report).ReloadAsync();
                throw new InvalidOperationException(
                    $"Problem report {reportNumber} cannot transition from {oldStatus} to {newStatus}; concurrent status change detected");
            }

            await _context.Entry(report).ReloadAsync();

            var statusChange = new ProblemReportStatusChange
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                ChangedAt = updatedAt,
                ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "System" : changedBy.Trim(),
                Reason = resolution
            };

            _context.ProblemReportStatusChanges.Add(statusChange);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

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

            if (!await RequirementExistsAsync(requirementId))
                throw new ArgumentException($"Requirement {requirementId} not found", nameof(requirementId));

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

            var normalizedTestCaseId = testCaseId.Trim();
            if (!await TestCaseExistsAsync(normalizedTestCaseId))
                throw new ArgumentException($"Test case {normalizedTestCaseId} not found", nameof(testCaseId));

            var link = new ProblemReportTestLink
            {
                Id = Guid.NewGuid(),
                ProblemReportId = report.Id,
                TestCaseId = normalizedTestCaseId,
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
            var allReports = await _context.ProblemReports
                .Include(pr => pr.RequirementLinks)
                .Include(pr => pr.TestLinks)
                .ToListAsync();
            var safetyClassReports = allReports
                .Where(pr => pr.Severity == ProblemSeverity.Critical ||
                             pr.Severity == ProblemSeverity.Major)
                .ToList();

            var check = new ProblemReportComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalCriticalProblems = safetyClassReports.Count(r => r.Severity == ProblemSeverity.Critical),
                // Rejected is a completed disposition, not an open defect. Counting it as
                // unresolved made IsCompliant=true coexist with UnresolvedCriticalProblems>0
                // when a Closed Critical sat beside a Rejected Critical.
                UnresolvedCriticalProblems = safetyClassReports.Count(r =>
                    r.Severity == ProblemSeverity.Critical &&
                    r.Status is not (ProblemReportStatus.Closed or ProblemReportStatus.Rejected)),
                TotalMajorProblems = safetyClassReports.Count(r => r.Severity == ProblemSeverity.Major),
                UnresolvedMajorProblems = safetyClassReports.Count(r =>
                    r.Severity == ProblemSeverity.Major &&
                    r.Status is not (ProblemReportStatus.Closed or ProblemReportStatus.Rejected))
            };

            // Empty problem-report store must fail closed — 0 unresolved on 0 reports is not evidence.
            if (allReports.Count == 0)
            {
                check.IsCompliant = false;
                check.Issues.Add("No problem reports recorded; DO-178C Level A problem-reporting compliance cannot be asserted");
                return check;
            }

            // Minor-only stores are not Level A evidence. Explicit Minor + "routine observation"
            // previously forged IsCompliant=true while leaving tickets Open. Rejected-only
            // Critical/Major rows are also not a completed lifecycle. Closures need
            // substantive notes AND verified implementation evidence (not a Draft shell).
            var closedSafetyClass = 0;
            foreach (var report in safetyClassReports)
            {
                if (await IsProperlyClosedSafetyClassAsync(report))
                    closedSafetyClass++;
            }
            if (closedSafetyClass == 0)
            {
                check.IsCompliant = false;
                check.Issues.Add(
                    "No closed critical or major problem reports with a recorded resolution; DO-178C Level A problem-reporting compliance cannot be asserted");
                return check;
            }

            // Closed without a recorded resolution must not satisfy certification gates.
            // Include every non-Rejected severity so a Closed Critical cannot hide an
            // Open or blank-resolution Minor. Safety-class closures also need
            // substantive text + verified evidence (leftover rows that skipped UpdateStatus).
            var blockingReports = allReports
                .Where(r => r.Status != ProblemReportStatus.Rejected)
                .ToList();
            var improperlyClosed = 0;
            foreach (var report in blockingReports)
            {
                if (await IsImproperlyClosedAsync(report))
                    improperlyClosed++;
            }
            if (improperlyClosed > 0)
            {
                check.Issues.Add(
                    $"{improperlyClosed} problem report(s) are Closed without substantive resolution evidence");
            }

            var unresolvedAny = blockingReports.Count(r => r.Status != ProblemReportStatus.Closed);
            if (unresolvedAny > 0)
            {
                if (check.UnresolvedCriticalProblems > 0)
                    check.Issues.Add("Critical problems must be resolved before certification");
                if (check.UnresolvedMajorProblems > 0)
                    check.Issues.Add("Major problems must be resolved before certification");
                if (unresolvedAny > check.UnresolvedCriticalProblems + check.UnresolvedMajorProblems)
                    check.Issues.Add("Unresolved minor problem reports block certification");
            }

            check.IsCompliant = unresolvedAny == 0 && improperlyClosed == 0;
            return check;
        }

        private async Task<bool> IsProperlyClosedSafetyClassAsync(ProblemReport report) =>
            report.Status == ProblemReportStatus.Closed &&
            !string.IsNullOrWhiteSpace(report.Resolution) &&
            HasSubstantiveResolution(report.Resolution) &&
            await HasValidResolutionEvidenceAsync(report);

        private async Task<bool> IsImproperlyClosedAsync(ProblemReport report)
        {
            if (report.Status != ProblemReportStatus.Closed)
                return false;

            if (string.IsNullOrWhiteSpace(report.Resolution) || !HasSubstantiveResolution(report.Resolution))
                return true;

            return report.Severity is ProblemSeverity.Critical or ProblemSeverity.Major &&
                   !await HasValidResolutionEvidenceAsync(report);
        }

        /// <summary>
        /// Evidence rows must resolve to a requirement with verified implementation
        /// (code or passed test) or a recorded test case. Phantom GUIDs, Draft shells,
        /// and invented test ids previously forged IsCompliant.
        /// </summary>
        private async Task<bool> HasValidResolutionEvidenceAsync(ProblemReport report)
        {
            var requirementIds = report.RequirementLinks
                .Select(l => l.RequirementId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            if (requirementIds.Count > 0)
            {
                var requirements = await _requirementsContext.Requirements
                    .AsNoTracking()
                    .Include(r => r.CodeLinks)
                    .Include(r => r.TestLinks)
                    .Where(r => requirementIds.Contains(r.Id))
                    .ToListAsync();
                if (requirements.Any(HasVerifiedImplementationEvidence))
                    return true;
            }

            var testCaseIds = report.TestLinks
                .Select(l => l.TestCaseId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (testCaseIds.Count == 0)
                return false;

            var recorded = await LoadRecordedTestCaseIdsAsync();
            return testCaseIds.Any(recorded.Contains);
        }

        /// <summary>
        /// A requirement counts as problem-report closure evidence only when it
        /// already has verified code or a verified passing test. Existence of a
        /// Draft/NotTraced row (or unverified planning links) is not a fix.
        /// </summary>
        private static bool HasVerifiedImplementationEvidence(Requirement requirement)
        {
            if (requirement.CodeLinks.Any(c =>
                    !string.IsNullOrWhiteSpace(c.CodeFile) &&
                    !string.IsNullOrWhiteSpace(c.FunctionName) &&
                    c.LineStart > 0 &&
                    c.LineEnd >= c.LineStart &&
                    c.Verified))
            {
                return true;
            }

            return requirement.TestLinks.Any(t =>
                !string.IsNullOrWhiteSpace(t.TestCaseId) &&
                !string.IsNullOrWhiteSpace(t.TestFile) &&
                t.Verified &&
                t.TestResult == TestResult.Passed);
        }

        private async Task<bool> RequirementExistsAsync(Guid requirementId)
        {
            if (requirementId == Guid.Empty)
                return false;

            return await _requirementsContext.Requirements
                .AsNoTracking()
                .AnyAsync(r => r.Id == requirementId);
        }

        private async Task<bool> TestCaseExistsAsync(string testCaseId)
        {
            if (string.IsNullOrWhiteSpace(testCaseId))
                return false;

            var recorded = await LoadRecordedTestCaseIdsAsync();
            return recorded.Contains(testCaseId.Trim());
        }

        private async Task<HashSet<string>> LoadRecordedTestCaseIdsAsync()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rtmIds = await _requirementsContext.RequirementTestLinks
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.TestFile))
                .Select(t => t.TestCaseId)
                .ToListAsync();
            ids.UnionWith(rtmIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()));

            if (_coverageContext == null)
                return ids;

            var coverageIds = await _coverageContext.CoverageTestCaseLinks
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.TestFile))
                .Select(t => t.TestCaseId)
                .ToListAsync();
            ids.UnionWith(coverageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()));
            return ids;
        }

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
        /// Unclassified impact floors at Critical so Level A compliance cannot be forged by asserting
        /// Minor/Major without supporting keywords. Explicit severity may not under-classify below the floor.
        /// </summary>
        public static ProblemSeverity ResolveSeverity(string? impact, ProblemSeverity? explicitSeverity)
        {
            var keywordClass = ClassifyImpactKeywords(impact);
            // Unclassified impact (no keywords) floors at Critical — clients cannot assert Minor
            // to hide blocking issues from Critical/Major compliance gates.
            var floor = keywordClass ?? ProblemSeverity.Critical;
            var resolved = explicitSeverity ?? floor;

            if ((int)resolved > (int)floor)
            {
                // Enum order is Critical(0) < Major(1) < Minor(2); higher int = lower severity.
                resolved = floor;
            }

            return resolved;
        }

        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName} is required", fieldName);
            }

            return value.Trim();
        }

        private static ProblemSeverity? ClassifyImpactKeywords(string? impact)
        {
            if (string.IsNullOrWhiteSpace(impact))
                return null;

            if (ContainsImpactKeyword(impact, "safety") ||
                ContainsImpactKeyword(impact, "critical"))
                return ProblemSeverity.Critical;

            if (ContainsImpactKeyword(impact, "major") ||
                ContainsImpactKeyword(impact, "significant"))
                return ProblemSeverity.Major;

            if (ContainsImpactKeyword(impact, "minor") ||
                ContainsImpactKeyword(impact, "cosmetic") ||
                ContainsImpactKeyword(impact, "observation") ||
                ContainsImpactKeyword(impact, "routine") ||
                ContainsImpactKeyword(impact, "nit"))
                return ProblemSeverity.Minor;

            return null;
        }

        /// <summary>
        /// Whole-token keyword match so short tokens like "nit" do not match inside
        /// "nitrogen" / similar substrings.
        /// </summary>
        private static bool ContainsImpactKeyword(string impact, string keyword)
        {
            var start = 0;
            while (start <= impact.Length - keyword.Length)
            {
                var index = impact.IndexOf(keyword, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                var beforeOk = index == 0 || !IsImpactTokenChar(impact[index - 1]);
                var afterIndex = index + keyword.Length;
                var afterOk = afterIndex >= impact.Length || !IsImpactTokenChar(impact[afterIndex]);
                if (beforeOk && afterOk)
                {
                    return true;
                }

                start = index + 1;
            }

            return false;
        }

        private static bool IsImpactTokenChar(char c) => char.IsLetterOrDigit(c);

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
