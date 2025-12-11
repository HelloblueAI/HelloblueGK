using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HB_NLP_Research_Lab.Certification
{
    /// <summary>
    /// Requirements Traceability System for DO-178C Level A / NASA NPR 7150.2 Class A
    /// Ensures every requirement is traced to design, code, and tests
    /// </summary>
    public class RequirementsTraceabilitySystem
    {
        private readonly RequirementsDbContext _context;
        private readonly ILogger<RequirementsTraceabilitySystem> _logger;

        public RequirementsTraceabilitySystem(RequirementsDbContext context, ILogger<RequirementsTraceabilitySystem> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new requirement with full traceability
        /// </summary>
        public async Task<Requirement> CreateRequirementAsync(Requirement requirement)
        {
            requirement.Id = Guid.NewGuid();
            requirement.CreatedAt = DateTime.UtcNow;
            requirement.Status = RequirementStatus.Draft;
            requirement.TraceabilityStatus = TraceabilityStatus.NotTraced;

            _context.Requirements.Add(requirement);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created requirement {RequirementId}: {Title}", requirement.Id, requirement.Title);
            return requirement;
        }

        /// <summary>
        /// Link requirement to design element
        /// </summary>
        public async Task LinkToDesignAsync(Guid requirementId, string designElementId, string designDocument)
        {
            var requirement = await _context.Requirements.FindAsync(requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = new RequirementDesignLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirementId,
                DesignElementId = designElementId,
                DesignDocument = designDocument,
                CreatedAt = DateTime.UtcNow,
                Verified = false
            };

            _context.RequirementDesignLinks.Add(link);
            await UpdateTraceabilityStatusAsync(requirementId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked requirement {RequirementId} to design {DesignElementId}", requirementId, designElementId);
        }

        /// <summary>
        /// Link requirement to code implementation
        /// </summary>
        public async Task LinkToCodeAsync(Guid requirementId, string codeFile, int lineStart, int lineEnd, string functionName)
        {
            var requirement = await _context.Requirements.FindAsync(requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = new RequirementCodeLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirementId,
                CodeFile = codeFile,
                LineStart = lineStart,
                LineEnd = lineEnd,
                FunctionName = functionName,
                CreatedAt = DateTime.UtcNow,
                Verified = false
            };

            _context.RequirementCodeLinks.Add(link);
            await UpdateTraceabilityStatusAsync(requirementId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked requirement {RequirementId} to code {CodeFile}:{LineStart}-{LineEnd}", 
                requirementId, codeFile, lineStart, lineEnd);
        }

        /// <summary>
        /// Link requirement to test case
        /// </summary>
        public async Task LinkToTestAsync(Guid requirementId, string testCaseId, string testFile, TestCoverageType coverageType)
        {
            var requirement = await _context.Requirements.FindAsync(requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = new RequirementTestLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirementId,
                TestCaseId = testCaseId,
                TestFile = testFile,
                CoverageType = coverageType,
                CreatedAt = DateTime.UtcNow,
                Verified = false,
                TestResult = TestResult.NotRun
            };

            _context.RequirementTestLinks.Add(link);
            await UpdateTraceabilityStatusAsync(requirementId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Linked requirement {RequirementId} to test {TestCaseId}", requirementId, testCaseId);
        }

        /// <summary>
        /// Generate Requirements Traceability Matrix (RTM) - Required for DO-178C Level A
        /// </summary>
        public async Task<RequirementsTraceabilityMatrix> GenerateRTMAsync()
        {
            var requirements = await _context.Requirements
                .Include(r => r.DesignLinks)
                .Include(r => r.CodeLinks)
                .Include(r => r.TestLinks)
                .ToListAsync();

            var matrix = new RequirementsTraceabilityMatrix
            {
                GeneratedAt = DateTime.UtcNow,
                Requirements = new List<RequirementTraceabilityEntry>()
            };

            foreach (var req in requirements)
            {
                var entry = new RequirementTraceabilityEntry
                {
                    RequirementId = req.Id,
                    RequirementNumber = req.RequirementNumber,
                    Title = req.Title,
                    Description = req.Description,
                    Status = req.Status,
                    TraceabilityStatus = req.TraceabilityStatus,
                    DesignElements = req.DesignLinks.Select(d => new DesignTrace
                    {
                        DesignElementId = d.DesignElementId,
                        DesignDocument = d.DesignDocument,
                        Verified = d.Verified
                    }).ToList(),
                    CodeImplementations = req.CodeLinks.Select(c => new CodeTrace
                    {
                        CodeFile = c.CodeFile,
                        LineRange = $"{c.LineStart}-{c.LineEnd}",
                        FunctionName = c.FunctionName,
                        Verified = c.Verified
                    }).ToList(),
                    TestCases = req.TestLinks.Select(t => new TestTrace
                    {
                        TestCaseId = t.TestCaseId,
                        TestFile = t.TestFile,
                        CoverageType = t.CoverageType,
                        TestResult = t.TestResult,
                        Verified = t.Verified
                    }).ToList()
                };

                matrix.Requirements.Add(entry);
            }

            // Calculate traceability metrics
            matrix.TotalRequirements = requirements.Count;
            matrix.FullyTracedRequirements = requirements.Count(r => r.TraceabilityStatus == TraceabilityStatus.FullyTraced);
            matrix.PartiallyTracedRequirements = requirements.Count(r => r.TraceabilityStatus == TraceabilityStatus.PartiallyTraced);
            matrix.UntracedRequirements = requirements.Count(r => r.TraceabilityStatus == TraceabilityStatus.NotTraced);
            matrix.TraceabilityPercentage = matrix.TotalRequirements > 0 
                ? (double)matrix.FullyTracedRequirements / matrix.TotalRequirements * 100 
                : 0;

            return matrix;
        }

        /// <summary>
        /// Verify requirement traceability completeness
        /// </summary>
        public async Task<TraceabilityVerificationReport> VerifyTraceabilityAsync()
        {
            var requirements = await _context.Requirements
                .Include(r => r.DesignLinks)
                .Include(r => r.CodeLinks)
                .Include(r => r.TestLinks)
                .ToListAsync();

            var report = new TraceabilityVerificationReport
            {
                VerifiedAt = DateTime.UtcNow,
                Issues = new List<TraceabilityIssue>()
            };

            foreach (var req in requirements)
            {
                // Check if requirement has design link
                if (!req.DesignLinks.Any())
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingDesignLink,
                        Severity = req.Priority == RequirementPriority.Critical ? IssueSeverity.Critical : IssueSeverity.Major,
                        Description = $"Requirement {req.RequirementNumber} has no design link"
                    });
                }

                // Check if requirement has code implementation
                if (!req.CodeLinks.Any())
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingCodeLink,
                        Severity = req.Priority == RequirementPriority.Critical ? IssueSeverity.Critical : IssueSeverity.Major,
                        Description = $"Requirement {req.RequirementNumber} has no code implementation"
                    });
                }

                // Check if requirement has test coverage
                if (!req.TestLinks.Any())
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingTestLink,
                        Severity = IssueSeverity.Critical, // Tests are always critical for Level A
                        Description = $"Requirement {req.RequirementNumber} has no test coverage"
                    });
                }

                // Check for MC/DC coverage for safety-critical requirements
                if (req.Priority == RequirementPriority.Critical && 
                    !req.TestLinks.Any(t => t.CoverageType == TestCoverageType.MCDC))
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingMCDCCoverage,
                        Severity = IssueSeverity.Critical,
                        Description = $"Critical requirement {req.RequirementNumber} lacks MC/DC coverage"
                    });
                }
            }

            report.TotalRequirements = requirements.Count;
            report.IssuesFound = report.Issues.Count;
            report.CriticalIssues = report.Issues.Count(i => i.Severity == IssueSeverity.Critical);
            report.IsCompliant = report.CriticalIssues == 0 && report.Issues.Count(i => i.Severity == IssueSeverity.Major) == 0;

            return report;
        }

        private async Task UpdateTraceabilityStatusAsync(Guid requirementId)
        {
            var requirement = await _context.Requirements
                .Include(r => r.DesignLinks)
                .Include(r => r.CodeLinks)
                .Include(r => r.TestLinks)
                .FirstOrDefaultAsync(r => r.Id == requirementId);

            if (requirement == null) return;

            bool hasDesign = requirement.DesignLinks.Any();
            bool hasCode = requirement.CodeLinks.Any();
            bool hasTest = requirement.TestLinks.Any();

            if (hasDesign && hasCode && hasTest)
            {
                requirement.TraceabilityStatus = TraceabilityStatus.FullyTraced;
            }
            else if (hasDesign || hasCode || hasTest)
            {
                requirement.TraceabilityStatus = TraceabilityStatus.PartiallyTraced;
            }
            else
            {
                requirement.TraceabilityStatus = TraceabilityStatus.NotTraced;
            }

            await _context.SaveChangesAsync();
        }
    }

    // Data Models
    public class Requirement
    {
        public Guid Id { get; set; }
        public string RequirementNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RequirementPriority Priority { get; set; }
        public RequirementStatus Status { get; set; }
        public TraceabilityStatus TraceabilityStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }

        // Navigation properties
        public List<RequirementDesignLink> DesignLinks { get; set; } = new();
        public List<RequirementCodeLink> CodeLinks { get; set; } = new();
        public List<RequirementTestLink> TestLinks { get; set; } = new();
    }

    public class RequirementDesignLink
    {
        public Guid Id { get; set; }
        public Guid RequirementId { get; set; }
        public string DesignElementId { get; set; } = string.Empty;
        public string DesignDocument { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool Verified { get; set; }
    }

    public class RequirementCodeLink
    {
        public Guid Id { get; set; }
        public Guid RequirementId { get; set; }
        public string CodeFile { get; set; } = string.Empty;
        public int LineStart { get; set; }
        public int LineEnd { get; set; }
        public string FunctionName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool Verified { get; set; }
    }

    public class RequirementTestLink
    {
        public Guid Id { get; set; }
        public Guid RequirementId { get; set; }
        public string TestCaseId { get; set; } = string.Empty;
        public string TestFile { get; set; } = string.Empty;
        public TestCoverageType CoverageType { get; set; }
        public TestResult TestResult { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Verified { get; set; }
    }

    public enum RequirementPriority
    {
        Critical,   // Safety-critical, requires MC/DC coverage
        High,       // Important functionality
        Medium,     // Standard functionality
        Low         // Nice-to-have
    }

    public enum RequirementStatus
    {
        Draft,
        UnderReview,
        Approved,
        Implemented,
        Verified,
        Closed
    }

    public enum TraceabilityStatus
    {
        NotTraced,
        PartiallyTraced,
        FullyTraced
    }

    public enum TestCoverageType
    {
        Statement,      // Statement coverage
        Branch,         // Branch coverage
        Condition,      // Condition coverage
        MCDC,           // Modified Condition/Decision Coverage (required for Level A)
        Path            // Path coverage
    }

    public enum TestResult
    {
        NotRun,
        Passed,
        Failed,
        Blocked
    }

    public class RequirementsTraceabilityMatrix
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalRequirements { get; set; }
        public int FullyTracedRequirements { get; set; }
        public int PartiallyTracedRequirements { get; set; }
        public int UntracedRequirements { get; set; }
        public double TraceabilityPercentage { get; set; }
        public List<RequirementTraceabilityEntry> Requirements { get; set; } = new();
    }

    public class RequirementTraceabilityEntry
    {
        public Guid RequirementId { get; set; }
        public string RequirementNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RequirementStatus Status { get; set; }
        public TraceabilityStatus TraceabilityStatus { get; set; }
        public List<DesignTrace> DesignElements { get; set; } = new();
        public List<CodeTrace> CodeImplementations { get; set; } = new();
        public List<TestTrace> TestCases { get; set; } = new();
    }

    public class DesignTrace
    {
        public string DesignElementId { get; set; } = string.Empty;
        public string DesignDocument { get; set; } = string.Empty;
        public bool Verified { get; set; }
    }

    public class CodeTrace
    {
        public string CodeFile { get; set; } = string.Empty;
        public string LineRange { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public bool Verified { get; set; }
    }

    public class TestTrace
    {
        public string TestCaseId { get; set; } = string.Empty;
        public string TestFile { get; set; } = string.Empty;
        public TestCoverageType CoverageType { get; set; }
        public TestResult TestResult { get; set; }
        public bool Verified { get; set; }
    }

    public class TraceabilityVerificationReport
    {
        public DateTime VerifiedAt { get; set; }
        public int TotalRequirements { get; set; }
        public int IssuesFound { get; set; }
        public int CriticalIssues { get; set; }
        public bool IsCompliant { get; set; }
        public List<TraceabilityIssue> Issues { get; set; } = new();
    }

    public class TraceabilityIssue
    {
        public Guid RequirementId { get; set; }
        public string RequirementNumber { get; set; } = string.Empty;
        public TraceabilityIssueType IssueType { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public enum TraceabilityIssueType
    {
        MissingDesignLink,
        MissingCodeLink,
        MissingTestLink,
        MissingMCDCCoverage,
        UnverifiedLink,
        BrokenLink
    }

    public enum IssueSeverity
    {
        Critical,   // Blocks certification
        Major,      // Must be fixed
        Minor,      // Should be fixed
        Info        // Informational
    }

    // DbContext for Requirements
    public class RequirementsDbContext : DbContext
    {
        public RequirementsDbContext(DbContextOptions<RequirementsDbContext> options) : base(options) { }

        public DbSet<Requirement> Requirements { get; set; }
        public DbSet<RequirementDesignLink> RequirementDesignLinks { get; set; }
        public DbSet<RequirementCodeLink> RequirementCodeLinks { get; set; }
        public DbSet<RequirementTestLink> RequirementTestLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Requirement>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RequirementNumber).IsUnique();
                entity.HasMany(e => e.DesignLinks).WithOne().HasForeignKey("RequirementId");
                entity.HasMany(e => e.CodeLinks).WithOne().HasForeignKey("RequirementId");
                entity.HasMany(e => e.TestLinks).WithOne().HasForeignKey("RequirementId");
            });
        }
    }
}
