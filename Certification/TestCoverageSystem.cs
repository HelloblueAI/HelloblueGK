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
        /// Link test case to code coverage
        /// </summary>
        public async Task LinkTestCaseAsync(string filePath, string testCaseId, string testFile, CoverageType coverageType)
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
        /// Generate coverage report for certification
        /// </summary>
        public async Task<CoverageReport> GenerateCoverageReportAsync()
        {
            var allCoverage = await _context.CodeCoverage
                .Include(c => c.TestCaseLinks)
                .ToListAsync();

            var report = new CoverageReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalFiles = allCoverage.Count,
                FilesWith100PercentStatementCoverage = allCoverage.Count(c => c.StatementCoverage >= 100.0),
                FilesWith100PercentBranchCoverage = allCoverage.Count(c => c.BranchCoverage >= 100.0),
                SafetyCriticalFiles = allCoverage.Count(c => c.IsSafetyCritical),
                SafetyCriticalFilesWithMCDC = allCoverage.Count(c => c.IsSafetyCritical && c.MCDCCoverage >= 100.0),
                Files = allCoverage.Select(c => new CoverageReportEntry
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

            // Calculate overall coverage
            if (allCoverage.Any())
            {
                report.OverallStatementCoverage = allCoverage.Average(c => c.StatementCoverage);
                report.OverallBranchCoverage = allCoverage.Average(c => c.BranchCoverage);
                report.OverallMCDCCoverage = allCoverage
                    .Where(c => c.IsSafetyCritical)
                    .DefaultIfEmpty()
                    .Average(c => c?.MCDCCoverage ?? 0);
            }

            // Fail closed: empty coverage DB / no safety-critical inventory must never
            // report DO-178C Level A compliance (vacuous MC/DC when SafetyCriticalFiles==0).
            report.MeetsDO178CLevelA = report.TotalFiles > 0 &&
                                      report.SafetyCriticalFiles > 0 &&
                                      report.FilesWith100PercentStatementCoverage == report.TotalFiles &&
                                      report.FilesWith100PercentBranchCoverage == report.TotalFiles &&
                                      report.SafetyCriticalFilesWithMCDC == report.SafetyCriticalFiles;

            // Identify gaps
            report.CoverageGaps = allCoverage
                .Where(c => !c.MeetsLevelARequirements)
                .Select(c => new CoverageGap
                {
                    FilePath = c.FilePath,
                    StatementCoverage = c.StatementCoverage,
                    BranchCoverage = c.BranchCoverage,
                    MCDCCoverage = c.MCDCCoverage,
                    IsSafetyCritical = c.IsSafetyCritical,
                    GapDescription = GenerateGapDescription(c)
                }).ToList();

            return report;
        }

        /// <summary>
        /// Verify coverage compliance for certification
        /// </summary>
        public async Task<CoverageComplianceCheck> VerifyComplianceAsync()
        {
            var allCoverage = await _context.CodeCoverage.ToListAsync();

            var check = new CoverageComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                TotalFiles = allCoverage.Count,
                FilesWith100PercentStatementCoverage = allCoverage.Count(c => c.StatementCoverage >= 100.0),
                FilesWith100PercentBranchCoverage = allCoverage.Count(c => c.BranchCoverage >= 100.0),
                SafetyCriticalFiles = allCoverage.Count(c => c.IsSafetyCritical),
                SafetyCriticalFilesWithMCDC = allCoverage.Count(c => c.IsSafetyCritical && c.MCDCCoverage >= 100.0)
            };

            // Fail closed when no coverage has been recorded — 0/0 must not imply compliance.
            if (check.TotalFiles == 0)
            {
                check.StatementCoverageCompliant = false;
                check.BranchCoverageCompliant = false;
                check.MCDCCoverageCompliant = false;
                check.IsCompliant = false;
                check.Issues.Add("No coverage data recorded; DO-178C Level A compliance cannot be asserted");
                return check;
            }

            // DO-178C Level A requirements — MC/DC cannot be vacuously true with zero safety-critical files.
            check.StatementCoverageCompliant = check.FilesWith100PercentStatementCoverage == check.TotalFiles;
            check.BranchCoverageCompliant = check.FilesWith100PercentBranchCoverage == check.TotalFiles;
            check.MCDCCoverageCompliant = check.SafetyCriticalFiles > 0 &&
                                         check.SafetyCriticalFilesWithMCDC == check.SafetyCriticalFiles;

            check.IsCompliant = check.StatementCoverageCompliant && 
                               check.BranchCoverageCompliant && 
                               check.MCDCCoverageCompliant;

            if (!check.IsCompliant)
            {
                if (!check.StatementCoverageCompliant)
                    check.Issues.Add($"Not all files have 100% statement coverage ({check.FilesWith100PercentStatementCoverage}/{check.TotalFiles})");
                
                if (!check.BranchCoverageCompliant)
                    check.Issues.Add($"Not all files have 100% branch coverage ({check.FilesWith100PercentBranchCoverage}/{check.TotalFiles})");
                
                if (check.SafetyCriticalFiles == 0)
                    check.Issues.Add("No safety-critical files marked; MC/DC compliance cannot be asserted");
                else if (!check.MCDCCoverageCompliant)
                    check.Issues.Add($"Not all safety-critical files have 100% MC/DC coverage ({check.SafetyCriticalFilesWithMCDC}/{check.SafetyCriticalFiles})");
            }

            return check;
        }

        private string GenerateGapDescription(CodeCoverage coverage)
        {
            var gaps = new List<string>();

            if (coverage.StatementCoverage < 100.0)
                gaps.Add($"{100.0 - coverage.StatementCoverage:F1}% statement coverage missing");

            if (coverage.BranchCoverage < 100.0)
                gaps.Add($"{100.0 - coverage.BranchCoverage:F1}% branch coverage missing");

            if (coverage.IsSafetyCritical && coverage.MCDCCoverage < 100.0)
                gaps.Add($"{100.0 - coverage.MCDCCoverage:F1}% MC/DC coverage missing (CRITICAL for safety-critical code)");

            return string.Join(", ", gaps);
        }

        private static string NormalizeFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Coverage file path is required.", nameof(filePath));
            }

            var normalized = filePath.Trim().Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.StartsWith("//", StringComparison.Ordinal)
                || (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'))
            {
                throw new ArgumentException("Coverage file path must be relative to the repository.", nameof(filePath));
            }

            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0
                || segments.Any(segment => segment is "." or ".."))
            {
                throw new ArgumentException("Coverage file path must not contain traversal segments.", nameof(filePath));
            }

            return string.Join("/", segments);
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
        public bool StatementCoverageCompliant { get; set; }
        public bool BranchCoverageCompliant { get; set; }
        public bool MCDCCoverageCompliant { get; set; }
        public bool IsCompliant { get; set; }
        public List<string> Issues { get; set; } = new();
    }

    // DbContext
    public class TestCoverageDbContext : DbContext
    {
        public TestCoverageDbContext(DbContextOptions<TestCoverageDbContext> options) : base(options) { }

        public DbSet<CodeCoverage> CodeCoverage { get; set; }
        public DbSet<CoverageTestCaseLink> CoverageTestCaseLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CodeCoverage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FilePath).IsUnique();
                entity.HasMany(e => e.TestCaseLinks).WithOne().HasForeignKey("CodeCoverageId");
            });
        }
    }
}
