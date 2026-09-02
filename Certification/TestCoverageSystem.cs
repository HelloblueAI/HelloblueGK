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
    /// Test Coverage System for DO-178C Level A / NASA NPR 7150.2 Class A
    /// Tracks code coverage including MC/DC (Modified Condition/Decision Coverage)
    /// Required: 100% statement coverage + MC/DC for safety-critical code
    /// </summary>
    public class TestCoverageSystem
    {
        private readonly TestCoverageDbContext _context;
        private readonly ILogger<TestCoverageSystem> _logger;

        public TestCoverageSystem(TestCoverageDbContext context, ILogger<TestCoverageSystem> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Record code coverage for a file
        /// </summary>
        public async Task RecordCoverageAsync(string filePath, CoverageMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(metrics);
            filePath = NormalizeFilePath(filePath);
            ValidateAndNormalizeMetrics(metrics);

            var coverage = await _context.CodeCoverage
                .FirstOrDefaultAsync(c => c.FilePath == filePath);

            if (coverage == null)
            {
                coverage = new CodeCoverage
                {
                    Id = Guid.NewGuid(),
                    FilePath = filePath,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CodeCoverage.Add(coverage);
            }

            coverage.StatementCoverage = metrics.StatementCoverage;
            coverage.BranchCoverage = metrics.BranchCoverage;
            coverage.ConditionCoverage = metrics.ConditionCoverage;
            coverage.MCDCCoverage = metrics.MCDCCoverage;
            coverage.PathCoverage = metrics.PathCoverage;
            coverage.TotalStatements = metrics.TotalStatements;
            coverage.CoveredStatements = metrics.CoveredStatements;
            coverage.TotalBranches = metrics.TotalBranches;
            coverage.CoveredBranches = metrics.CoveredBranches;
            coverage.TotalConditions = metrics.TotalConditions;
            coverage.CoveredConditions = metrics.CoveredConditions;
            coverage.LastUpdated = DateTime.UtcNow;

            // Determine if file meets Level A requirements
            coverage.MeetsLevelARequirements = coverage.StatementCoverage >= 100.0 && 
                                              coverage.BranchCoverage >= 100.0 &&
                                              (!coverage.IsSafetyCritical || coverage.MCDCCoverage >= 100.0);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Recorded coverage for {FilePath}: {StatementCoverage}% statements, {BranchCoverage}% branches", 
                LogSanitizer.Sanitize(filePath), coverage.StatementCoverage, coverage.BranchCoverage);
        }

        /// <summary>
        /// Mark file as safety-critical (requires MC/DC coverage)
        /// </summary>
        public async Task MarkAsSafetyCriticalAsync(string filePath, bool isSafetyCritical)
        {
            filePath = NormalizeFilePath(filePath);

            var coverage = await _context.CodeCoverage
                .FirstOrDefaultAsync(c => c.FilePath == filePath);

            if (coverage == null)
            {
                coverage = new CodeCoverage
                {
                    Id = Guid.NewGuid(),
                    FilePath = filePath,
                    IsSafetyCritical = isSafetyCritical,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CodeCoverage.Add(coverage);
            }
            else
            {
                coverage.IsSafetyCritical = isSafetyCritical;
                coverage.LastUpdated = DateTime.UtcNow;
            }

            // Recompute Level A gate after the safety-critical flag changes so MC/DC
            // requirements apply immediately (and clear if the flag is removed).
            coverage.MeetsLevelARequirements = coverage.StatementCoverage >= 100.0 &&
                                              coverage.BranchCoverage >= 100.0 &&
                                              (!coverage.IsSafetyCritical || coverage.MCDCCoverage >= 100.0);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked {FilePath} as safety-critical: {IsSafetyCritical}", LogSanitizer.Sanitize(filePath), isSafetyCritical);
        }

        /// <summary>
        /// Link test case to code coverage. Links are official Level A execution evidence —
        /// empty IDs, traversal paths, or files outside Tests/ cannot satisfy compliance.
        /// </summary>
        public async Task LinkTestCaseAsync(string filePath, string testCaseId, string testFile, CoverageType coverageType)
        {
            if (string.IsNullOrWhiteSpace(testCaseId))
                throw new ArgumentException("Test case id is required", nameof(testCaseId));
            if (string.IsNullOrWhiteSpace(testFile))
                throw new ArgumentException("Test file is required", nameof(testFile));

            filePath = NormalizeFilePath(filePath);
            testCaseId = NormalizeTestCaseId(testCaseId);
            testFile = NormalizeTestFilePath(testFile);

            var coverage = await _context.CodeCoverage
                .FirstOrDefaultAsync(c => c.FilePath == filePath);

            if (coverage == null)
            {
                coverage = new CodeCoverage
                {
                    Id = Guid.NewGuid(),
                    FilePath = filePath,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CodeCoverage.Add(coverage);
                await _context.SaveChangesAsync();
            }

            var link = new CoverageTestCaseLink
            {
                Id = Guid.NewGuid(),
                CodeCoverageId = coverage.Id,
                TestCaseId = testCaseId,
                TestFile = testFile,
                CoverageType = coverageType,
                CreatedAt = DateTime.UtcNow
            };

            _context.CoverageTestCaseLinks.Add(link);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Register (or re-activate) a server-owned required coverage file.
        /// Compliance scope is never accepted from client RecordCoverage inventories alone.
        /// </summary>
        public async Task<RequiredCoverageFile> RegisterRequiredFileAsync(
            string filePath,
            bool isSafetyCritical = true,
            string? registeredBy = null)
        {
            var normalized = NormalizeFilePath(filePath);
            var rosterRows = await _context.RequiredCoverageFiles.ToListAsync();
            var matches = MatchingStoredCoveragePaths(rosterRows, normalized);
            var existing = matches.FirstOrDefault(f =>
                               string.Equals(f.FilePath, normalized, StringComparison.Ordinal))
                           ?? matches
                               .OrderByDescending(f => f.IsActive)
                               .ThenByDescending(f => f.RegisteredAt)
                               .FirstOrDefault();
            if (existing != null)
            {
                existing.IsActive = true;
                // Keep the stored path. Recasing a leftover case-variant hits the unique
                // FilePath index and desynchronizes exact CodeCoverage lookups.
                existing.IsSafetyCritical = isSafetyCritical;
                existing.RegisteredBy = string.IsNullOrWhiteSpace(registeredBy)
                    ? existing.RegisteredBy
                    : registeredBy.Trim();
                existing.RegisteredAt = DateTime.UtcNow;
                foreach (var duplicate in matches.Where(f => f.Id != existing.Id))
                    duplicate.IsActive = false;
                await _context.SaveChangesAsync();
                return existing;
            }

            var required = new RequiredCoverageFile
            {
                Id = Guid.NewGuid(),
                FilePath = normalized,
                IsSafetyCritical = isSafetyCritical,
                IsActive = true,
                RegisteredBy = string.IsNullOrWhiteSpace(registeredBy) ? null : registeredBy.Trim(),
                RegisteredAt = DateTime.UtcNow
            };
            _context.RequiredCoverageFiles.Add(required);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Registered required coverage file {FilePath} (safetyCritical={IsSafetyCritical})",
                LogSanitizer.Sanitize(normalized),
                isSafetyCritical);
            return required;
        }

        /// <summary>
        /// Revoke a required coverage file so it no longer participates in compliance scope.
        /// </summary>
        public async Task RevokeRequiredFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            // Lookup by canonical form only — leftover scheme/outside-tree rows must
            // still be revocable. Register remains the path that rejects unsafe paths.
            var matches = MatchingStoredCoveragePaths(
                await _context.RequiredCoverageFiles.ToListAsync(),
                filePath);
            if (matches.Count == 0)
                throw new ArgumentException($"Required coverage file '{filePath.Trim()}' not found", nameof(filePath));

            foreach (var existing in matches)
                existing.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Revoked required coverage file {FilePath}",
                LogSanitizer.Sanitize(matches[0].FilePath));
        }

        /// <summary>
        /// Generate coverage report for certification against the server-owned roster.
        /// </summary>
        public async Task<CoverageReport> GenerateCoverageReportAsync()
        {
            var roster = await _context.RequiredCoverageFiles
                .Where(f => f.IsActive)
                .ToListAsync();
            var coverageRows = await _context.CodeCoverage
                .Include(c => c.TestCaseLinks)
                .ToListAsync();

            var rosterCoverage = new List<CodeCoverage>();
            foreach (var required in roster)
            {
                if (!IsStoredCoveragePathSafe(required.FilePath))
                    continue;

                if (TryGetCoverageForRoster(coverageRows, required.FilePath, out var coverage)
                    && coverage != null
                    && IsStoredCoveragePathSafe(coverage.FilePath))
                {
                    // Roster owns the safety-critical flag for Level A MC/DC scope.
                    coverage.IsSafetyCritical = required.IsSafetyCritical;
                    // Recompute leftover stored percentages from counts so a pre-gate
                    // 100% row with 5/10 statements cannot stamp Level A.
                    if (!TryApplyStoredCoverageIntegrity(coverage))
                        continue;

                    rosterCoverage.Add(coverage);
                }
            }

            var report = new CoverageReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalFiles = roster.Count,
                FilesWith100PercentStatementCoverage = rosterCoverage.Count(c => c.StatementCoverage >= 100.0),
                FilesWith100PercentBranchCoverage = rosterCoverage.Count(c => c.BranchCoverage >= 100.0),
                SafetyCriticalFiles = roster.Count(r => r.IsSafetyCritical),
                SafetyCriticalFilesWithMCDC = rosterCoverage.Count(c => c.IsSafetyCritical && c.MCDCCoverage >= 100.0),
                Files = rosterCoverage.Select(c => new CoverageReportEntry
                {
                    FilePath = c.FilePath,
                    StatementCoverage = c.StatementCoverage,
                    BranchCoverage = c.BranchCoverage,
                    ConditionCoverage = c.ConditionCoverage,
                    MCDCCoverage = c.MCDCCoverage,
                    IsSafetyCritical = c.IsSafetyCritical,
                    MeetsLevelARequirements = c.MeetsLevelARequirements,
                    TestCaseCount = c.TestCaseLinks.Count
                }).ToList()
            };

            if (rosterCoverage.Count > 0)
            {
                report.OverallStatementCoverage = rosterCoverage.Average(c => c.StatementCoverage);
                report.OverallBranchCoverage = rosterCoverage.Average(c => c.BranchCoverage);
                report.OverallMCDCCoverage = rosterCoverage
                    .Where(c => c.IsSafetyCritical)
                    .DefaultIfEmpty()
                    .Average(c => c?.MCDCCoverage ?? 0);
            }

            // Fail closed: empty roster / missing evidence / no safety-critical inventory /
            // leftover scheme, absolute, or outside-tree paths / leftover count-free or
            // count-mismatched percentages / count-only records with no linked tests.
            var missingRosterFiles = roster.Count(r =>
                !TryGetCoverageForRoster(coverageRows, r.FilePath, out _));
            var unsafeRosterFiles = roster.Count(r => !IsStoredCoveragePathSafe(r.FilePath));
            var inconsistentRosterFiles = roster.Count(r =>
                TryGetCoverageForRoster(coverageRows, r.FilePath, out var coverage)
                && coverage != null
                && IsStoredCoveragePathSafe(r.FilePath)
                && IsStoredCoveragePathSafe(coverage.FilePath)
                && !HasCountableCoverageTotals(coverage));
            report.FilesWithTestEvidence = rosterCoverage.Count(HasValidTestEvidence);
            report.SafetyCriticalFilesWithMcdcTestEvidence = rosterCoverage.Count(c =>
                c.IsSafetyCritical && HasValidMcdcTestEvidence(c));
            report.MeetsDO178CLevelA = roster.Count > 0 &&
                                      missingRosterFiles == 0 &&
                                      unsafeRosterFiles == 0 &&
                                      inconsistentRosterFiles == 0 &&
                                      report.SafetyCriticalFiles > 0 &&
                                      report.FilesWith100PercentStatementCoverage == report.TotalFiles &&
                                      report.FilesWith100PercentBranchCoverage == report.TotalFiles &&
                                      report.SafetyCriticalFilesWithMCDC == report.SafetyCriticalFiles &&
                                      report.FilesWithTestEvidence == report.TotalFiles &&
                                      report.SafetyCriticalFilesWithMcdcTestEvidence == report.SafetyCriticalFiles;

            report.CoverageGaps = roster
                .Select(required =>
                {
                    TryGetCoverageForRoster(coverageRows, required.FilePath, out var coverage);
                    if (!IsStoredCoveragePathSafe(required.FilePath)
                        || (coverage != null && !IsStoredCoveragePathSafe(coverage.FilePath)))
                    {
                        return new CoverageGap
                        {
                            FilePath = required.FilePath,
                            IsSafetyCritical = required.IsSafetyCritical,
                            GapDescription = "Unsafe coverage evidence path"
                        };
                    }

                    if (coverage == null)
                    {
                        return new CoverageGap
                        {
                            FilePath = required.FilePath,
                            IsSafetyCritical = required.IsSafetyCritical,
                            GapDescription = "No coverage evidence recorded for required file"
                        };
                    }

                    if (!HasCountableCoverageTotals(coverage))
                    {
                        return new CoverageGap
                        {
                            FilePath = required.FilePath,
                            IsSafetyCritical = required.IsSafetyCritical,
                            GapDescription = "Count-inconsistent or count-free coverage evidence"
                        };
                    }

                    var description = GenerateGapDescription(coverage);
                    if (string.IsNullOrEmpty(description))
                    {
                        return null;
                    }

                    return new CoverageGap
                    {
                        FilePath = coverage.FilePath,
                        StatementCoverage = coverage.StatementCoverage,
                        BranchCoverage = coverage.BranchCoverage,
                        MCDCCoverage = coverage.MCDCCoverage,
                        IsSafetyCritical = coverage.IsSafetyCritical,
                        GapDescription = description
                    };
                })
                .Where(gap => gap != null)
                .Select(gap => gap!)
                .ToList();

            return report;
        }

        /// <summary>
        /// Verify coverage compliance against the server-owned required-file roster.
        /// Client-invented coverage rows outside the roster cannot forge IsCompliant.
        /// </summary>
        public async Task<CoverageComplianceCheck> VerifyComplianceAsync()
        {
            var roster = await _context.RequiredCoverageFiles
                .Where(f => f.IsActive)
                .ToListAsync();
            var coverageRows = await _context.CodeCoverage
                .Include(c => c.TestCaseLinks)
                .ToListAsync();

            var rosterCoverage = new List<CodeCoverage>();
            var missingFiles = new List<string>();
            var unsafeFiles = new List<string>();
            var inconsistentFiles = new List<string>();
            foreach (var required in roster)
            {
                if (!IsStoredCoveragePathSafe(required.FilePath))
                {
                    unsafeFiles.Add(required.FilePath);
                    continue;
                }

                if (!TryGetCoverageForRoster(coverageRows, required.FilePath, out var coverage)
                    || coverage == null
                    || !IsStoredCoveragePathSafe(coverage.FilePath))
                {
                    missingFiles.Add(required.FilePath);
                    continue;
                }

                coverage.IsSafetyCritical = required.IsSafetyCritical;
                if (!TryApplyStoredCoverageIntegrity(coverage))
                {
                    inconsistentFiles.Add(required.FilePath);
                    continue;
                }

                rosterCoverage.Add(coverage);
            }

            var check = new CoverageComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalFiles = roster.Count,
                FilesWith100PercentStatementCoverage = rosterCoverage.Count(c => c.StatementCoverage >= 100.0),
                FilesWith100PercentBranchCoverage = rosterCoverage.Count(c => c.BranchCoverage >= 100.0),
                SafetyCriticalFiles = roster.Count(r => r.IsSafetyCritical),
                SafetyCriticalFilesWithMCDC = rosterCoverage.Count(c => c.IsSafetyCritical && c.MCDCCoverage >= 100.0),
                FilesWithTestEvidence = rosterCoverage.Count(HasValidTestEvidence),
                SafetyCriticalFilesWithMcdcTestEvidence = rosterCoverage.Count(c =>
                    c.IsSafetyCritical && HasValidMcdcTestEvidence(c))
            };

            // Fail closed when the server roster is empty — cherry-picked client files must not imply compliance.
            if (check.TotalFiles == 0)
            {
                check.StatementCoverageCompliant = false;
                check.BranchCoverageCompliant = false;
                check.MCDCCoverageCompliant = false;
                check.IsCompliant = false;
                check.Issues.Add("Required coverage roster is empty; DO-178C Level A compliance cannot be asserted");
                return check;
            }

            if (unsafeFiles.Count > 0)
            {
                check.StatementCoverageCompliant = false;
                check.BranchCoverageCompliant = false;
                check.MCDCCoverageCompliant = false;
                check.IsCompliant = false;
                check.Issues.Add($"{unsafeFiles.Count} required coverage file(s) are outside the repository implementation or test trees");
                foreach (var unsafePath in unsafeFiles)
                {
                    check.Issues.Add($"Unsafe coverage evidence path: {unsafePath}");
                }

                return check;
            }

            if (missingFiles.Count > 0)
            {
                check.StatementCoverageCompliant = false;
                check.BranchCoverageCompliant = false;
                check.MCDCCoverageCompliant = false;
                check.IsCompliant = false;
                check.Issues.Add($"{missingFiles.Count} required coverage file(s) have no recorded evidence");
                foreach (var missing in missingFiles)
                {
                    check.Issues.Add($"Missing coverage evidence: {missing}");
                }

                return check;
            }

            if (inconsistentFiles.Count > 0)
            {
                check.StatementCoverageCompliant = false;
                check.BranchCoverageCompliant = false;
                check.MCDCCoverageCompliant = false;
                check.IsCompliant = false;
                check.Issues.Add(
                    $"{inconsistentFiles.Count} required coverage file(s) have count-inconsistent or count-free percentage evidence");
                foreach (var inconsistent in inconsistentFiles)
                {
                    check.Issues.Add($"Count-inconsistent or count-free coverage evidence: {inconsistent}");
                }

                return check;
            }

            check.StatementCoverageCompliant = check.FilesWith100PercentStatementCoverage == check.TotalFiles;
            check.BranchCoverageCompliant = check.FilesWith100PercentBranchCoverage == check.TotalFiles;
            check.MCDCCoverageCompliant = check.SafetyCriticalFiles > 0 &&
                                         check.SafetyCriticalFilesWithMCDC == check.SafetyCriticalFiles;
            check.TestEvidenceCompliant = check.FilesWithTestEvidence == check.TotalFiles &&
                                         check.SafetyCriticalFiles > 0 &&
                                         check.SafetyCriticalFilesWithMcdcTestEvidence == check.SafetyCriticalFiles;

            check.IsCompliant = check.StatementCoverageCompliant &&
                               check.BranchCoverageCompliant &&
                               check.MCDCCoverageCompliant &&
                               check.TestEvidenceCompliant;

            if (!check.IsCompliant)
            {
                if (!check.StatementCoverageCompliant)
                    check.Issues.Add($"Not all required files have 100% statement coverage ({check.FilesWith100PercentStatementCoverage}/{check.TotalFiles})");

                if (!check.BranchCoverageCompliant)
                    check.Issues.Add($"Not all required files have 100% branch coverage ({check.FilesWith100PercentBranchCoverage}/{check.TotalFiles})");

                if (check.SafetyCriticalFiles == 0)
                    check.Issues.Add("No safety-critical files on the required coverage roster; MC/DC compliance cannot be asserted");
                else if (!check.MCDCCoverageCompliant)
                    check.Issues.Add($"Not all safety-critical required files have 100% MC/DC coverage ({check.SafetyCriticalFilesWithMCDC}/{check.SafetyCriticalFiles})");

                if (check.FilesWithTestEvidence < check.TotalFiles)
                    check.Issues.Add($"Not all required files have linked test-case evidence ({check.FilesWithTestEvidence}/{check.TotalFiles})");
                else if (check.SafetyCriticalFiles > 0 &&
                         check.SafetyCriticalFilesWithMcdcTestEvidence < check.SafetyCriticalFiles)
                    check.Issues.Add($"Not all safety-critical required files have an MC/DC test-case link ({check.SafetyCriticalFilesWithMcdcTestEvidence}/{check.SafetyCriticalFiles})");
            }

            return check;
        }

        private static List<RequiredCoverageFile> MatchingStoredCoveragePaths(
            IEnumerable<RequiredCoverageFile> rows,
            string lookupPath)
        {
            return rows
                .Where(f => StoredCoveragePathsMatch(f.FilePath, lookupPath))
                .ToList();
        }

        private static bool TryGetCoverageForRoster(
            IReadOnlyCollection<CodeCoverage> coverageRows,
            string rosterPath,
            out CodeCoverage? coverage)
        {
            var matches = coverageRows
                .Where(c => StoredCoveragePathsMatch(c.FilePath, rosterPath))
                .ToList();
            coverage = matches.FirstOrDefault(c =>
                           string.Equals(c.FilePath, rosterPath, StringComparison.Ordinal))
                       ?? matches.FirstOrDefault();
            return coverage != null;
        }

        private static bool StoredCoveragePathsMatch(string? left, string? right) =>
            string.Equals(
                CanonicalizeStoredCoveragePath(left),
                CanonicalizeStoredCoveragePath(right),
                StringComparison.OrdinalIgnoreCase);

        private string GenerateGapDescription(CodeCoverage coverage)
        {
            var gaps = new List<string>();

            if (coverage.StatementCoverage < 100.0)
                gaps.Add($"{100.0 - coverage.StatementCoverage:F1}% statement coverage missing");

            if (coverage.BranchCoverage < 100.0)
                gaps.Add($"{100.0 - coverage.BranchCoverage:F1}% branch coverage missing");

            if (coverage.IsSafetyCritical && coverage.MCDCCoverage < 100.0)
                gaps.Add($"{100.0 - coverage.MCDCCoverage:F1}% MC/DC coverage missing (CRITICAL for safety-critical code)");

            if (!HasValidTestEvidence(coverage))
                gaps.Add("No linked test-case evidence for required file");
            else if (coverage.IsSafetyCritical && !HasValidMcdcTestEvidence(coverage))
                gaps.Add("Safety-critical file has no MC/DC test-case link");

            return string.Join(", ", gaps);
        }

        private static readonly HashSet<string> AllowedCoverageRoots = new(StringComparer.OrdinalIgnoreCase)
        {
            "Core", "WebAPI", "Certification", "Physics", "AI", "Models", "Aerospace", "Scripts", "Tests"
        };

        /// <summary>
        /// Recompute leftover stored percentages from counts using the same rules as
        /// <see cref="RecordCoverageAsync"/>. Count-free or contradictory totals cannot
        /// satisfy Level A even when StatementCoverage/MCDCCoverage were persisted as 100.
        /// </summary>
        private static bool TryApplyStoredCoverageIntegrity(CodeCoverage coverage)
        {
            if (!HasCountableCoverageTotals(coverage))
                return false;

            coverage.StatementCoverage = (double)coverage.CoveredStatements / coverage.TotalStatements * 100.0;
            coverage.BranchCoverage = (double)coverage.CoveredBranches / coverage.TotalBranches * 100.0;
            coverage.ConditionCoverage = coverage.TotalConditions > 0
                ? (double)coverage.CoveredConditions / coverage.TotalConditions * 100.0
                : 0.0;

            if (coverage.TotalConditions > 0)
            {
                var claimedMcdc = coverage.MCDCCoverage;
                if (double.IsNaN(claimedMcdc) || double.IsInfinity(claimedMcdc) || claimedMcdc < 0)
                    claimedMcdc = 0;
                else if (claimedMcdc > 100)
                    claimedMcdc = 100;

                coverage.MCDCCoverage = Math.Min(claimedMcdc, coverage.ConditionCoverage);
            }
            else
            {
                coverage.MCDCCoverage = 0.0;
            }

            coverage.MeetsLevelARequirements = coverage.StatementCoverage >= 100.0 &&
                                              coverage.BranchCoverage >= 100.0 &&
                                              (!coverage.IsSafetyCritical || coverage.MCDCCoverage >= 100.0);
            return true;
        }

        private static bool HasCountableCoverageTotals(CodeCoverage coverage)
        {
            if (coverage.TotalStatements <= 0 || coverage.TotalBranches <= 0 || coverage.TotalConditions < 0)
                return false;

            if (coverage.CoveredStatements < 0 || coverage.CoveredBranches < 0 || coverage.CoveredConditions < 0)
                return false;

            return coverage.CoveredStatements <= coverage.TotalStatements
                && coverage.CoveredBranches <= coverage.TotalBranches
                && coverage.CoveredConditions <= coverage.TotalConditions;
        }

        private static bool HasValidTestEvidence(CodeCoverage coverage) =>
            coverage.TestCaseLinks.Any(IsValidTestCaseLink);

        private static bool HasValidMcdcTestEvidence(CodeCoverage coverage) =>
            coverage.TestCaseLinks.Any(link =>
                IsValidTestCaseLink(link) && link.CoverageType == CoverageType.MCDC);

        private static bool IsValidTestCaseLink(CoverageTestCaseLink link)
        {
            if (string.IsNullOrWhiteSpace(link.TestCaseId) || string.IsNullOrWhiteSpace(link.TestFile))
            {
                return false;
            }

            try
            {
                var normalized = NormalizeFilePath(link.TestFile);
                return normalized.StartsWith("Tests/", StringComparison.Ordinal);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string NormalizeTestCaseId(string testCaseId)
        {
            if (string.IsNullOrWhiteSpace(testCaseId))
            {
                throw new ArgumentException("Test case id is required.", nameof(testCaseId));
            }

            return testCaseId.Trim();
        }

        private static string NormalizeTestFilePath(string testFile)
        {
            string normalized;
            try
            {
                normalized = NormalizeFilePath(testFile);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message, nameof(testFile), ex);
            }

            if (!normalized.StartsWith("Tests/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Coverage test file must be a repository-relative path under Tests/.",
                    nameof(testFile));
            }

            return normalized;
        }

        private static string NormalizeFilePath(string filePath)
        {
            if (!TryNormalizeCoveragePath(filePath, out var normalized, out var error))
            {
                throw new ArgumentException(error, nameof(filePath));
            }

            return normalized;
        }

        private static string CanonicalizeStoredCoveragePath(string? filePath) =>
            (filePath ?? string.Empty).Trim().Replace('\\', '/');

        private static bool IsStoredCoveragePathSafe(string? filePath)
        {
            return TryNormalizeCoveragePath(filePath, out var normalized, out _)
                && string.Equals(filePath, normalized, StringComparison.Ordinal);
        }

        private static bool TryNormalizeCoveragePath(string? filePath, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "Coverage file path is required.";
                return false;
            }

            var trimmed = filePath.Trim().Replace('\\', '/');
            // Reject absolute / UNC / scheme URIs (http://, file:, C:\) so Level A
            // coverage evidence cannot point outside the repository.
            if (trimmed.StartsWith("/", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.Contains("://", StringComparison.Ordinal)
                || trimmed.Contains(':', StringComparison.Ordinal))
            {
                error = "Coverage file path must be relative to the repository.";
                return false;
            }

            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0
                || segments.Any(segment => segment is "." or ".."))
            {
                error = "Coverage file path must not contain traversal segments.";
                return false;
            }

            if (segments.Length < 2 || !AllowedCoverageRoots.Contains(segments[0]))
            {
                error = "Coverage file path must be under an implementation or test tree (Core/, WebAPI/, Certification/, Physics/, AI/, Models/, Aerospace/, Scripts/, Tests/).";
                return false;
            }

            normalized = string.Join("/", segments);
            return true;
        }

        private static void ValidateAndNormalizeMetrics(CoverageMetrics metrics)
        {
            ValidateCoveragePair(metrics.CoveredStatements, metrics.TotalStatements, nameof(metrics.CoveredStatements), nameof(metrics.TotalStatements));
            ValidateCoveragePair(metrics.CoveredBranches, metrics.TotalBranches, nameof(metrics.CoveredBranches), nameof(metrics.TotalBranches));
            ValidateCoveragePair(metrics.CoveredConditions, metrics.TotalConditions, nameof(metrics.CoveredConditions), nameof(metrics.TotalConditions));

            // Level A evidence requires countable statement/branch totals — percentage-only
            // records with zero totals previously forged 100% compliance.
            if (metrics.TotalStatements <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(metrics.TotalStatements),
                    "Coverage records require a positive TotalStatements count.");
            }

            if (metrics.TotalBranches <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(metrics.TotalBranches),
                    "Coverage records require a positive TotalBranches count.");
            }

            // Server-compute percentages from counts so clients cannot assert 100% while
            // covered/total counts disagree.
            metrics.StatementCoverage = (double)metrics.CoveredStatements / metrics.TotalStatements * 100.0;
            metrics.BranchCoverage = (double)metrics.CoveredBranches / metrics.TotalBranches * 100.0;

            metrics.ConditionCoverage = metrics.TotalConditions > 0
                ? (double)metrics.CoveredConditions / metrics.TotalConditions * 100.0
                : 0.0;

            // MC/DC cannot be client-asserted without condition evidence. When condition
            // totals exist, cap claimed MC/DC by measured condition coverage.
            if (metrics.TotalConditions > 0)
            {
                var claimedMcdc = NormalizePercentage(metrics.MCDCCoverage, nameof(metrics.MCDCCoverage));
                metrics.MCDCCoverage = Math.Min(claimedMcdc, metrics.ConditionCoverage);
            }
            else
            {
                metrics.MCDCCoverage = 0.0;
            }

            metrics.PathCoverage = NormalizePercentage(metrics.PathCoverage, nameof(metrics.PathCoverage));
        }

        private static void ValidateCoveragePair(int covered, int total, string coveredName, string totalName)
        {
            if (total < 0)
            {
                throw new ArgumentOutOfRangeException(totalName, "Coverage totals cannot be negative.");
            }

            if (covered < 0)
            {
                throw new ArgumentOutOfRangeException(coveredName, "Covered counts cannot be negative.");
            }

            if (covered > total)
            {
                throw new ArgumentOutOfRangeException(coveredName, "Covered counts cannot exceed totals.");
            }
        }

        private static double NormalizePercentage(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(name, "Coverage percentages must be between 0 and 100.");
            }

            return value;
        }
    }

    // Data Models
    public class CodeCoverage
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public bool IsSafetyCritical { get; set; }
        public double StatementCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double ConditionCoverage { get; set; }
        public double MCDCCoverage { get; set; }
        public double PathCoverage { get; set; }
        public int TotalStatements { get; set; }
        public int CoveredStatements { get; set; }
        public int TotalBranches { get; set; }
        public int CoveredBranches { get; set; }
        public int TotalConditions { get; set; }
        public int CoveredConditions { get; set; }
        public bool MeetsLevelARequirements { get; set; }
        public DateTime LastUpdated { get; set; }

        public List<CoverageTestCaseLink> TestCaseLinks { get; set; } = new();
    }

    public class CoverageTestCaseLink
    {
        public Guid Id { get; set; }
        public Guid CodeCoverageId { get; set; }
        public string TestCaseId { get; set; } = string.Empty;
        public string TestFile { get; set; } = string.Empty;
        public CoverageType CoverageType { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CoverageMetrics
    {
        public double StatementCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double ConditionCoverage { get; set; }
        public double MCDCCoverage { get; set; }
        public double PathCoverage { get; set; }
        public int TotalStatements { get; set; }
        public int CoveredStatements { get; set; }
        public int TotalBranches { get; set; }
        public int CoveredBranches { get; set; }
        public int TotalConditions { get; set; }
        public int CoveredConditions { get; set; }
    }

    public enum CoverageType
    {
        Statement,
        Branch,
        Condition,
        MCDC,
        Path
    }

    public class CoverageReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalFiles { get; set; }
        public int FilesWith100PercentStatementCoverage { get; set; }
        public int FilesWith100PercentBranchCoverage { get; set; }
        public int SafetyCriticalFiles { get; set; }
        public int SafetyCriticalFilesWithMCDC { get; set; }
        public int FilesWithTestEvidence { get; set; }
        public int SafetyCriticalFilesWithMcdcTestEvidence { get; set; }
        public double OverallStatementCoverage { get; set; }
        public double OverallBranchCoverage { get; set; }
        public double OverallMCDCCoverage { get; set; }
        public bool MeetsDO178CLevelA { get; set; }
        public List<CoverageReportEntry> Files { get; set; } = new();
        public List<CoverageGap> CoverageGaps { get; set; } = new();
    }

    public class CoverageReportEntry
    {
        public string FilePath { get; set; } = string.Empty;
        public double StatementCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double ConditionCoverage { get; set; }
        public double MCDCCoverage { get; set; }
        public bool IsSafetyCritical { get; set; }
        public bool MeetsLevelARequirements { get; set; }
        public int TestCaseCount { get; set; }
    }

    public class CoverageGap
    {
        public string FilePath { get; set; } = string.Empty;
        public double StatementCoverage { get; set; }
        public double BranchCoverage { get; set; }
        public double MCDCCoverage { get; set; }
        public bool IsSafetyCritical { get; set; }
        public string GapDescription { get; set; } = string.Empty;
    }

    public class CoverageComplianceCheck
    {
        public DateTime CheckedAt { get; set; }
        public int TotalFiles { get; set; }
        public int FilesWith100PercentStatementCoverage { get; set; }
        public int FilesWith100PercentBranchCoverage { get; set; }
        public int SafetyCriticalFiles { get; set; }
        public int SafetyCriticalFilesWithMCDC { get; set; }
        public int FilesWithTestEvidence { get; set; }
        public int SafetyCriticalFilesWithMcdcTestEvidence { get; set; }
        public bool StatementCoverageCompliant { get; set; }
        public bool BranchCoverageCompliant { get; set; }
        public bool MCDCCoverageCompliant { get; set; }
        public bool TestEvidenceCompliant { get; set; }
        public bool IsCompliant { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    /// <summary>
    /// Server-owned inventory of files that must have Level A coverage evidence.
    /// Compliance scope is derived from this store — never from client-invented coverage rows alone.
    /// </summary>
    public class RequiredCoverageFile
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public bool IsSafetyCritical { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string? RegisteredBy { get; set; }
        public DateTime RegisteredAt { get; set; }
    }

    // DbContext
    public class TestCoverageDbContext : DbContext
    {
        public TestCoverageDbContext(DbContextOptions<TestCoverageDbContext> options) : base(options) { }

        public DbSet<CodeCoverage> CodeCoverage { get; set; }
        public DbSet<CoverageTestCaseLink> CoverageTestCaseLinks { get; set; }
        public DbSet<RequiredCoverageFile> RequiredCoverageFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CodeCoverage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FilePath).IsUnique();
                entity.HasMany(e => e.TestCaseLinks).WithOne().HasForeignKey("CodeCoverageId");
            });

            modelBuilder.Entity<RequiredCoverageFile>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FilePath).IsUnique();
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1024);
            });
        }
    }
}
