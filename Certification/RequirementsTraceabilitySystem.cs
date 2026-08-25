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
            ArgumentNullException.ThrowIfNull(requirement);
            requirement.RequirementNumber = NormalizeRequirementNumber(requirement.RequirementNumber);
            requirement.Title = NormalizeRequiredText(requirement.Title, "Title");
            requirement.Description = NormalizeRequiredText(requirement.Description, "Description");

            // Priority is fail-closed: unclassified defaults to Critical (MC/DC required),
            // and safety/critical/hazard keywords cannot be under-classified to skip Level A gates.
            requirement.Priority = ResolvePriority(
                requirement.RequirementNumber,
                requirement.Title,
                requirement.Description,
                requirement.Priority);

            var numberTaken = await _context.Requirements
                .AnyAsync(r => r.RequirementNumber == requirement.RequirementNumber);
            if (numberTaken)
            {
                throw new ArgumentException(
                    $"Requirement number '{requirement.RequirementNumber}' is already in use",
                    nameof(requirement));
            }

            requirement.Id = Guid.NewGuid();
            requirement.CreatedAt = DateTime.UtcNow;
            requirement.Status = RequirementStatus.Draft;
            requirement.TraceabilityStatus = TraceabilityStatus.NotTraced;

            _context.Requirements.Add(requirement);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _context.Entry(requirement).State = EntityState.Detached;
                throw new ArgumentException(
                    $"Requirement number '{requirement.RequirementNumber}' is already in use",
                    nameof(requirement));
            }

            _logger.LogInformation("Created requirement {RequirementId}: {Title}", requirement.Id, LogSanitizer.Sanitize(requirement.Title));
            return requirement;
        }

        /// <summary>
        /// Resolve requirement priority with a keyword floor.
        /// Unclassified priority defaults to Critical so MC/DC cannot be skipped by omission.
        /// Explicit Medium/Low may not under-classify safety/critical/hazard wording.
        /// </summary>
        public static RequirementPriority ResolvePriority(
            string? requirementNumber,
            string? title,
            string? description,
            RequirementPriority? explicitPriority)
        {
            var keywordFloor = ClassifyPriorityKeywords(requirementNumber, title, description);
            var resolved = explicitPriority ?? keywordFloor ?? RequirementPriority.Critical;

            // Enum order is Critical(0) < High(1) < Medium(2) < Low(3); higher int = lower priority.
            if (keywordFloor.HasValue && (int)resolved > (int)keywordFloor.Value)
            {
                resolved = keywordFloor.Value;
            }

            return resolved;
        }

        private static RequirementPriority? ClassifyPriorityKeywords(
            string? requirementNumber,
            string? title,
            string? description)
        {
            if (ContainsPriorityKeyword(requirementNumber, title, description,
                    "safety", "critical", "catastrophic", "hazard", "unsafe", "fatal"))
            {
                return RequirementPriority.Critical;
            }

            return null;
        }

        private static bool ContainsPriorityKeyword(string? requirementNumber, string? title, string? description, params string[] keywords)
        {
            return new[] { requirementNumber, title, description }
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Any(text => keywords.Any(keyword =>
                    text!.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Link requirement to design element
        /// </summary>
        public async Task<RequirementDesignLink> LinkToDesignAsync(Guid requirementId, string designElementId, string designDocument)
        {
            if (string.IsNullOrWhiteSpace(designElementId))
                throw new ArgumentException("Design element id is required", nameof(designElementId));
            if (string.IsNullOrWhiteSpace(designDocument))
                throw new ArgumentException("Design document is required", nameof(designDocument));

            var normalizedDesignDocument = NormalizeEvidencePath(designDocument, nameof(designDocument));

            var requirement = await _context.Requirements.FindAsync(requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = new RequirementDesignLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirementId,
                DesignElementId = designElementId.Trim(),
                DesignDocument = normalizedDesignDocument,
                CreatedAt = DateTime.UtcNow,
                Verified = false
            };

            _context.RequirementDesignLinks.Add(link);
            await _context.SaveChangesAsync();
            await UpdateTraceabilityStatusAsync(requirementId);

            _logger.LogInformation("Linked requirement {RequirementId} to design {DesignElementId}", requirementId, designElementId);
            return link;
        }

        /// <summary>
        /// Link requirement to code implementation
        /// </summary>
        public async Task<RequirementCodeLink> LinkToCodeAsync(Guid requirementId, string codeFile, int lineStart, int lineEnd, string functionName)
        {
            if (string.IsNullOrWhiteSpace(codeFile))
                throw new ArgumentException("Code file is required", nameof(codeFile));
            if (string.IsNullOrWhiteSpace(functionName))
                throw new ArgumentException("Function name is required", nameof(functionName));
            if (lineStart <= 0 || lineEnd < lineStart)
                throw new ArgumentException("Code line range must be a positive, ordered span");

            var normalizedCodeFile = NormalizeEvidencePath(codeFile, nameof(codeFile));

            var requirement = await _context.Requirements.FindAsync(requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = new RequirementCodeLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirementId,
                CodeFile = normalizedCodeFile,
                LineStart = lineStart,
                LineEnd = lineEnd,
                FunctionName = functionName.Trim(),
                CreatedAt = DateTime.UtcNow,
                Verified = false
            };

            _context.RequirementCodeLinks.Add(link);
            await _context.SaveChangesAsync();
            await UpdateTraceabilityStatusAsync(requirementId);

            _logger.LogInformation("Linked requirement {RequirementId} to code {CodeFile}:{LineStart}-{LineEnd}", 
                requirementId, LogSanitizer.Sanitize(codeFile), lineStart, lineEnd);
            return link;
        }

        /// <summary>
        /// Link requirement to test case
        /// </summary>
        public async Task<RequirementTestLink> LinkToTestAsync(Guid requirementId, string testCaseId, string testFile, TestCoverageType coverageType)
        {
            if (string.IsNullOrWhiteSpace(testCaseId))
                throw new ArgumentException("Test case id is required", nameof(testCaseId));
            if (string.IsNullOrWhiteSpace(testFile))
                throw new ArgumentException("Test file is required", nameof(testFile));

            var normalizedTestFile = NormalizeEvidencePath(testFile, nameof(testFile));

            var requirement = await _context.Requirements.FindAsync(requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = new RequirementTestLink
            {
                Id = Guid.NewGuid(),
                RequirementId = requirementId,
                TestCaseId = testCaseId.Trim(),
                TestFile = normalizedTestFile,
                CoverageType = coverageType,
                CreatedAt = DateTime.UtcNow,
                Verified = false,
                TestResult = TestResult.NotRun
            };

            _context.RequirementTestLinks.Add(link);
            await _context.SaveChangesAsync();
            await UpdateTraceabilityStatusAsync(requirementId);

            _logger.LogInformation("Linked requirement {RequirementId} to test {TestCaseId}", requirementId, LogSanitizer.SanitizeIdentifier(testCaseId));
            return link;
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
                Requirements = requirements.Select(req => new RequirementTraceabilityEntry
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
                }).ToList()
            };

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
                // Re-score leftover Medium/Low rows whose title/description hid hazard language.
                var effectivePriority = ResolvePriority(
                    req.RequirementNumber,
                    req.Title,
                    req.Description,
                    req.Priority);
                var isCritical = effectivePriority == RequirementPriority.Critical;

                var hasDesign = HasMeaningfulDesignLinks(req);
                var hasCode = HasMeaningfulCodeLinks(req);
                var hasTest = HasMeaningfulTestLinks(req);

                // Check if requirement has design link
                if (!hasDesign)
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingDesignLink,
                        Severity = isCritical ? IssueSeverity.Critical : IssueSeverity.Major,
                        Description = $"Requirement {req.RequirementNumber} has no design link"
                    });
                }
                else if (!HasVerifiedDesignLinks(req))
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingDesignLink,
                        Severity = isCritical ? IssueSeverity.Critical : IssueSeverity.Major,
                        Description = $"Requirement {req.RequirementNumber} has no verified design link"
                    });
                }

                // Check if requirement has code implementation
                if (!hasCode)
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingCodeLink,
                        Severity = isCritical ? IssueSeverity.Critical : IssueSeverity.Major,
                        Description = $"Requirement {req.RequirementNumber} has no code implementation"
                    });
                }
                else if (!HasVerifiedCodeLinks(req))
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingCodeLink,
                        Severity = isCritical ? IssueSeverity.Critical : IssueSeverity.Major,
                        Description = $"Requirement {req.RequirementNumber} has no verified code link"
                    });
                }

                // Check if requirement has test coverage
                if (!hasTest)
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
                else if (!HasPassedVerifiedTestLinks(req))
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingTestLink,
                        Severity = IssueSeverity.Critical,
                        Description = $"Requirement {req.RequirementNumber} has no verified passed test evidence"
                    });
                }

                // Check for MC/DC coverage for safety-critical requirements.
                // CoverageType alone is client-asserted — require verified Passed MC/DC evidence.
                if (isCritical &&
                    !req.TestLinks.Any(t =>
                        t.CoverageType == TestCoverageType.MCDC &&
                        t.Verified &&
                        t.TestResult == TestResult.Passed &&
                        !string.IsNullOrWhiteSpace(t.TestCaseId) &&
                        !string.IsNullOrWhiteSpace(t.TestFile)))
                {
                    report.Issues.Add(new TraceabilityIssue
                    {
                        RequirementId = req.Id,
                        RequirementNumber = req.RequirementNumber,
                        IssueType = TraceabilityIssueType.MissingMCDCCoverage,
                        Severity = IssueSeverity.Critical,
                        Description = $"Critical requirement {req.RequirementNumber} lacks verified passed MC/DC coverage"
                    });
                }
            }

            report.TotalRequirements = requirements.Count;
            report.IssuesFound = report.Issues.Count;
            report.CriticalIssues = report.Issues.Count(i => i.Severity == IssueSeverity.Critical);

            // Empty RTM must fail closed — 0 critical/major issues with 0 requirements is not Level A evidence.
            if (report.TotalRequirements == 0)
            {
                report.IsCompliant = false;
                report.Issues.Add(new TraceabilityIssue
                {
                    RequirementId = Guid.Empty,
                    RequirementNumber = "(none)",
                    IssueType = TraceabilityIssueType.MissingDesignLink,
                    Severity = IssueSeverity.Critical,
                    Description = "No requirements recorded; DO-178C Level A traceability compliance cannot be asserted"
                });
                report.IssuesFound = report.Issues.Count;
                report.CriticalIssues = report.Issues.Count(i => i.Severity == IssueSeverity.Critical);
                return report;
            }

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

            // FullyTraced requires verified (and for tests, passed) evidence — link presence alone is not enough.
            bool hasDesign = HasVerifiedDesignLinks(requirement);
            bool hasCode = HasVerifiedCodeLinks(requirement);
            bool hasTest = HasPassedVerifiedTestLinks(requirement);
            bool hasAnyLink = HasMeaningfulDesignLinks(requirement) ||
                              HasMeaningfulCodeLinks(requirement) ||
                              HasMeaningfulTestLinks(requirement);

            if (hasDesign && hasCode && hasTest)
            {
                requirement.TraceabilityStatus = TraceabilityStatus.FullyTraced;
            }
            else if (hasAnyLink)
            {
                requirement.TraceabilityStatus = TraceabilityStatus.PartiallyTraced;
            }
            else
            {
                requirement.TraceabilityStatus = TraceabilityStatus.NotTraced;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Mark an existing design/code/test link as verified (server-owned disposition step).
        /// </summary>
        public async Task VerifyLinkAsync(Guid requirementId, Guid linkId, RequirementLinkKind linkKind)
        {
            var requirement = await _context.Requirements
                .Include(r => r.DesignLinks)
                .Include(r => r.CodeLinks)
                .Include(r => r.TestLinks)
                .FirstOrDefaultAsync(r => r.Id == requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            switch (linkKind)
            {
                case RequirementLinkKind.Design:
                {
                    var link = requirement.DesignLinks.FirstOrDefault(d => d.Id == linkId);
                    if (link == null)
                        throw new ArgumentException($"Design link {linkId} not found");
                    if (!HasMeaningfulDesignLink(link))
                        throw new InvalidOperationException("Cannot verify a vacuous design link");
                    link.Verified = true;
                    break;
                }
                case RequirementLinkKind.Code:
                {
                    var link = requirement.CodeLinks.FirstOrDefault(c => c.Id == linkId);
                    if (link == null)
                        throw new ArgumentException($"Code link {linkId} not found");
                    if (!HasMeaningfulCodeLink(link))
                        throw new InvalidOperationException("Cannot verify a vacuous code link");
                    link.Verified = true;
                    break;
                }
                case RequirementLinkKind.Test:
                {
                    var link = requirement.TestLinks.FirstOrDefault(t => t.Id == linkId);
                    if (link == null)
                        throw new ArgumentException($"Test link {linkId} not found");
                    if (!HasMeaningfulTestLink(link))
                        throw new InvalidOperationException("Cannot verify a vacuous test link");
                    if (link.TestResult != TestResult.Passed)
                        throw new InvalidOperationException("Cannot verify a test link that has not Passed");
                    link.Verified = true;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(linkKind));
            }

            await _context.SaveChangesAsync();
            await UpdateTraceabilityStatusAsync(requirementId);
        }

        /// <summary>
        /// Record an execution result for a test link. Passing is required before verify.
        /// </summary>
        public async Task RecordTestResultAsync(Guid requirementId, Guid testLinkId, TestResult testResult)
        {
            var requirement = await _context.Requirements
                .Include(r => r.TestLinks)
                .FirstOrDefaultAsync(r => r.Id == requirementId);
            if (requirement == null)
                throw new ArgumentException($"Requirement {requirementId} not found");

            var link = requirement.TestLinks.FirstOrDefault(t => t.Id == testLinkId);
            if (link == null)
                throw new ArgumentException($"Test link {testLinkId} not found");

            link.TestResult = testResult;
            if (testResult != TestResult.Passed)
            {
                // Failed/NotRun/Blocked evidence cannot remain verified.
                link.Verified = false;
            }

            await _context.SaveChangesAsync();
            await UpdateTraceabilityStatusAsync(requirementId);
        }

        private static bool HasMeaningfulDesignLinks(Requirement requirement) =>
            requirement.DesignLinks.Any(HasMeaningfulDesignLink);

        private static bool HasMeaningfulCodeLinks(Requirement requirement) =>
            requirement.CodeLinks.Any(HasMeaningfulCodeLink);

        private static bool HasMeaningfulTestLinks(Requirement requirement) =>
            requirement.TestLinks.Any(HasMeaningfulTestLink);

        private static bool HasVerifiedDesignLinks(Requirement requirement) =>
            requirement.DesignLinks.Any(d => HasMeaningfulDesignLink(d) && d.Verified);

        private static bool HasVerifiedCodeLinks(Requirement requirement) =>
            requirement.CodeLinks.Any(c => HasMeaningfulCodeLink(c) && c.Verified);

        private static bool HasPassedVerifiedTestLinks(Requirement requirement) =>
            requirement.TestLinks.Any(t =>
                HasMeaningfulTestLink(t) && t.Verified && t.TestResult == TestResult.Passed);

        private static bool HasMeaningfulDesignLink(RequirementDesignLink d) =>
            !string.IsNullOrWhiteSpace(d.DesignElementId) &&
            !string.IsNullOrWhiteSpace(d.DesignDocument);

        private static bool HasMeaningfulCodeLink(RequirementCodeLink c) =>
            !string.IsNullOrWhiteSpace(c.CodeFile) &&
            !string.IsNullOrWhiteSpace(c.FunctionName) &&
            c.LineStart > 0 &&
            c.LineEnd >= c.LineStart;

        private static bool HasMeaningfulTestLink(RequirementTestLink t) =>
            !string.IsNullOrWhiteSpace(t.TestCaseId) &&
            !string.IsNullOrWhiteSpace(t.TestFile);

        private static string NormalizeRequirementNumber(string? requirementNumber)
        {
            var normalized = NormalizeRequiredText(requirementNumber, "RequirementNumber");
            if (normalized.Contains("..", StringComparison.Ordinal) ||
                normalized.IndexOfAny(['/', '\\']) >= 0)
            {
                throw new ArgumentException(
                    "Requirement number must not contain path segments.",
                    nameof(requirementNumber));
            }

            return normalized;
        }

        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName} is required", fieldName);
            }

            var trimmed = value.Trim();
            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException($"{fieldName} must not contain control characters", fieldName);
            }

            return trimmed;
        }

        private static string NormalizeEvidencePath(string path, string paramName)
        {
            var normalized = path.Trim().Replace('\\', '/');
            // Reject absolute / UNC / scheme URIs (http://, file:, C:\) so RTM
            // evidence cannot point outside the repository.
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.StartsWith("//", StringComparison.Ordinal)
                || normalized.Contains("://", StringComparison.Ordinal)
                || normalized.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException("Evidence path must be relative to the repository.", paramName);
            }

            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0
                || segments.Any(segment => segment is "." or ".."))
            {
                throw new ArgumentException("Evidence path must not contain traversal segments.", paramName);
            }

            return string.Join("/", segments);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            for (var inner = exception.InnerException; inner != null; inner = inner.InnerException)
            {
                var message = inner.Message;
                if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public enum RequirementLinkKind
    {
        Design,
        Code,
        Test
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
