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
            report.Severity = ResolveSeverity(report.Title, report.Description, report.Impact, explicitSeverity);

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

            // Re-score leftover rows so "routine observation of catastrophic failure"
            // stored as Minor cannot skip Critical/Major closure + evidence gates.
            var safetyClassReports = allReports
                .Select(r => (Report: r, Severity: EffectiveSeverity(r)))
                .Where(x => x.Severity is ProblemSeverity.Critical or ProblemSeverity.Major)
                .ToList();

            var check = new ProblemReportComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalCriticalProblems = safetyClassReports.Count(r => r.Severity == ProblemSeverity.Critical),
                // Rejected is a completed disposition, not an open defect.
                UnresolvedCriticalProblems = safetyClassReports.Count(r =>
                    r.Severity == ProblemSeverity.Critical &&
                    r.Report.Status is not (ProblemReportStatus.Closed or ProblemReportStatus.Rejected)),
                TotalMajorProblems = safetyClassReports.Count(r => r.Severity == ProblemSeverity.Major),
                UnresolvedMajorProblems = safetyClassReports.Count(r =>
                    r.Severity == ProblemSeverity.Major &&
                    r.Report.Status is not (ProblemReportStatus.Closed or ProblemReportStatus.Rejected))
            };

            // Empty problem-report store must fail closed — 0 unresolved on 0 reports is not evidence.
            if (allReports.Count == 0)
            {
                check.IsCompliant = false;
                check.Issues.Add("No problem reports recorded; DO-178C Level A problem-reporting compliance cannot be asserted");
                return check;
            }

            // Closures need substantive notes AND verified implementation evidence
            // (not a Draft/NotTraced shell). Leftover Minor rows with safety language
            // cannot skip those Critical/Major gates.
            var underclassified = allReports.Count(r =>
                r.Severity == ProblemSeverity.Minor &&
                EffectiveSeverity(r) is ProblemSeverity.Critical or ProblemSeverity.Major);
            if (underclassified > 0)
            {
                check.Issues.Add(
                    $"{underclassified} problem report(s) store Minor severity while title/description/impact contain elevated safety language");
            }

            var closedSafetyClass = 0;
            foreach (var item in safetyClassReports)
            {
                if (await IsProperlyClosedSafetyClassAsync(item.Report))
                    closedSafetyClass++;
            }
            if (closedSafetyClass == 0)
            {
                check.Issues.Add(
                    "No closed critical or major problem reports with a recorded resolution; DO-178C Level A problem-reporting compliance cannot be asserted");
            }

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

            check.IsCompliant = closedSafetyClass > 0 &&
                                unresolvedAny == 0 &&
                                improperlyClosed == 0 &&
                                underclassified == 0;
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

            return EffectiveSeverity(report) is ProblemSeverity.Critical or ProblemSeverity.Major &&
                   !await HasValidResolutionEvidenceAsync(report);
        }

        /// <summary>
        /// Evidence rows must resolve to a requirement with verified implementation
        /// (verified code span or verified Passed test) or a coverage-recorded test case.
        /// Phantom GUIDs, Draft shells, and invented test ids previously forged IsCompliant.
        /// RTM RequirementTestLinks are planning assertions and must not bootstrap inventory.
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

            // Coverage is the execution inventory. RTM RequirementTestLinks are
            // planning rows — treating them as recorded tests let an invented
            // Tests/*.cs link close Critical/Major reports and stamp IsCompliant.
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
        public static ProblemSeverity ResolveSeverity(string? impact, ProblemSeverity? explicitSeverity) =>
            ResolveSeverity(title: null, description: null, impact, explicitSeverity);

        /// <summary>
        /// Resolve severity from title, description, and impact. High-severity language in any field
        /// is a floor so "routine observation of catastrophic failure" cannot be stored as Minor.
        /// Minor keywords apply only to Impact so a title like "Routine stand checkout" cannot
        /// downgrade unclassified impact below the fail-closed Critical default.
        /// </summary>
        public static ProblemSeverity ResolveSeverity(
            string? title,
            string? description,
            string? impact,
            ProblemSeverity? explicitSeverity)
        {
            var keywordClass = ClassifyReportKeywords(title, description, impact);
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

        internal static ProblemSeverity EffectiveSeverity(ProblemReport report)
        {
            ArgumentNullException.ThrowIfNull(report);
            return ResolveSeverity(report.Title, report.Description, report.Impact, report.Severity);
        }

        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName} is required", fieldName);
            }

            return value.Trim();
        }

        // Same stems/inflections as FormalCodeReviewSystem so "hazardous" / "critically"
        // still elevate when paired with routine/observation. Only the standalone word
        // "non" (non-critical / non critical) suppresses elevation — not "cannon critical".
        private const string StandaloneNonPrefix = @"(?<!(?<![A-Za-z0-9])non[- ]?)";

        private static readonly Regex CriticalElevationPattern = new(
            StandaloneNonPrefix + @"(?<![A-Za-z0-9])(safety|safeties|critical(?:ly|ity)?|catastrophic(?:ally)?|hazard(?:s|ous|ously)?|fail(?:ure|ures|ed)?|loss(?:es)?|lost|unsafe(?:ly)?|fatal(?:ly|ity|ities)?)(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex MajorElevationPattern = new(
            StandaloneNonPrefix + @"(?<![A-Za-z0-9])(major(?:ly)?|significant(?:ly)?|significance)(?![A-Za-z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static ProblemSeverity? ClassifyReportKeywords(string? title, string? description, string? impact)
        {
            var elevationText = $"{title} {description} {impact}";

            if (!string.IsNullOrWhiteSpace(elevationText) && CriticalElevationPattern.IsMatch(elevationText))
            {
                return ProblemSeverity.Critical;
            }

            if (!string.IsNullOrWhiteSpace(elevationText) && MajorElevationPattern.IsMatch(elevationText))
            {
                return ProblemSeverity.Major;
            }

            // Minor tokens are Impact-only. Title/description "routine" must not hide
            // unclassified impact (fail-closed Critical) from compliance gates.
            if (!string.IsNullOrWhiteSpace(impact) &&
                (ContainsImpactKeyword(impact, "minor") ||
                 ContainsImpactKeyword(impact, "cosmetic") ||
                 ContainsImpactKeyword(impact, "observation") ||
                 ContainsImpactKeyword(impact, "routine") ||
                 ContainsImpactKeyword(impact, "nit")))
            {
                return ProblemSeverity.Minor;
            }

            return null;
        }

        /// <summary>
        /// Whole-token keyword match so short tokens like "nit" do not match inside
        /// "nitrogen" / similar substrings.
        /// </summary>
        private static bool ContainsImpactKeyword(string impact, string keyword) =>
            ContainsKeyword(impact, keyword, skipNegated: false);

        private static bool ContainsKeyword(string text, string keyword, bool skipNegated)
        {
            var start = 0;
            while (start <= text.Length - keyword.Length)
            {
                var index = text.IndexOf(keyword, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                var beforeOk = index == 0 || !IsImpactTokenChar(text[index - 1]);
                var afterIndex = index + keyword.Length;
                var afterOk = afterIndex >= text.Length || !IsImpactTokenChar(text[afterIndex]);
                if (beforeOk && afterOk && !(skipNegated && IsNegatedKeyword(text, index)))
                {
                    return true;
                }

                start = index + 1;
            }

            return false;
        }

        /// <summary>
        /// "non-critical" / "non critical" must not count as the Critical token.
        /// </summary>
        private static bool IsNegatedKeyword(string text, int keywordIndex)
        {
            var i = keywordIndex;
            while (i > 0 && (text[i - 1] == '-' || char.IsWhiteSpace(text[i - 1])))
            {
                i--;
            }

            return i >= 3 &&
                   text.AsSpan(i - 3, 3).Equals("non", StringComparison.OrdinalIgnoreCase) &&
                   (i == 3 || !IsImpactTokenChar(text[i - 4]));
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
