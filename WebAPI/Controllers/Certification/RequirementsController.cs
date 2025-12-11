using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Models;
using System.Linq.Expressions;

namespace HB_NLP_Research_Lab.WebAPI.Controllers.Certification;

/// <summary>
/// Requirements Management Controller for Flight Software Certification
/// DO-178C Level A / NASA NPR 7150.2 Class A
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/certification/requirements")]
[ApiVersion("1.0")]
[Authorize]
[Tags("Certification - Requirements")]
public class RequirementsController : ControllerBase
{
    private readonly RequirementsTraceabilitySystem _rts;
    private readonly RequirementsDbContext _context;
    private readonly ILogger<RequirementsController> _logger;

    public RequirementsController(
        RequirementsTraceabilitySystem rts,
        RequirementsDbContext context,
        ILogger<RequirementsController> logger)
    {
        _rts = rts;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a new requirement
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RequirementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRequirement([FromBody] CreateRequirementRequest request)
    {
        try
        {
            var priority = Enum.TryParse<RequirementPriority>(request.Priority, out var parsedPriority) 
                ? parsedPriority 
                : RequirementPriority.Medium;

            var requirement = new Requirement
            {
                RequirementNumber = request.RequirementNumber,
                Title = request.Title,
                Description = request.Description,
                Priority = priority,
                CreatedBy = User.Identity?.Name ?? "System"
            };

            var created = await _rts.CreateRequirementAsync(requirement);

            return CreatedAtAction(nameof(GetRequirement), new { id = created.Id }, 
                new RequirementResponse
                {
                    Id = created.Id,
                    RequirementNumber = created.RequirementNumber,
                    Title = created.Title,
                    Description = created.Description,
                    Priority = created.Priority.ToString(),
                    Status = created.Status.ToString(),
                    TraceabilityStatus = created.TraceabilityStatus.ToString()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating requirement");
            return StatusCode(500, new ErrorResponse
            {
                StatusCode = 500,
                Message = "An error occurred while creating the requirement",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Get requirement by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RequirementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequirement(Guid id)
    {
        var requirement = await _context.Requirements
            .Include(r => r.DesignLinks)
            .Include(r => r.CodeLinks)
            .Include(r => r.TestLinks)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (requirement == null)
            return NotFound();

        return Ok(new RequirementResponse
        {
            Id = requirement.Id,
            RequirementNumber = requirement.RequirementNumber,
            Title = requirement.Title,
            Description = requirement.Description,
            Priority = requirement.Priority.ToString(),
            Status = requirement.Status.ToString(),
            TraceabilityStatus = requirement.TraceabilityStatus.ToString(),
            DesignLinks = requirement.DesignLinks.Select(d => new DesignLinkResponse
            {
                DesignElementId = d.DesignElementId,
                DesignDocument = d.DesignDocument,
                Verified = d.Verified
            }).ToList(),
            CodeLinks = requirement.CodeLinks.Select(c => new CodeLinkResponse
            {
                CodeFile = c.CodeFile,
                LineRange = $"{c.LineStart}-{c.LineEnd}",
                FunctionName = c.FunctionName,
                Verified = c.Verified
            }).ToList(),
            TestLinks = requirement.TestLinks.Select(t => new TestLinkResponse
            {
                TestCaseId = t.TestCaseId,
                TestFile = t.TestFile,
                CoverageType = t.CoverageType.ToString(),
                TestResult = t.TestResult.ToString(),
                Verified = t.Verified
            }).ToList()
        });
    }

    /// <summary>
    /// Link requirement to code implementation
    /// </summary>
    [HttpPost("{id}/link-code")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkToCode(Guid id, [FromBody] LinkCodeRequest request)
    {
        try
        {
            await _rts.LinkToCodeAsync(id, request.CodeFile, request.LineStart, request.LineEnd, request.FunctionName);
            return Ok(new { message = "Requirement linked to code successfully" });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Link requirement to test case
    /// </summary>
    [HttpPost("{id}/link-test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkToTest(Guid id, [FromBody] LinkTestRequest request)
    {
        try
        {
            var coverageType = Enum.Parse<TestCoverageType>(request.CoverageType);
            await _rts.LinkToTestAsync(id, request.TestCaseId, request.TestFile, coverageType);
            return Ok(new { message = "Requirement linked to test successfully" });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Generate Requirements Traceability Matrix (RTM)
    /// </summary>
    [HttpGet("rtm")]
    [ProducesResponseType(typeof(RequirementsTraceabilityMatrixResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateRTM()
    {
        var rtm = await _rts.GenerateRTMAsync();
        return Ok(new RequirementsTraceabilityMatrixResponse
        {
            GeneratedAt = rtm.GeneratedAt,
            TotalRequirements = rtm.TotalRequirements,
            FullyTracedRequirements = rtm.FullyTracedRequirements,
            PartiallyTracedRequirements = rtm.PartiallyTracedRequirements,
            UntracedRequirements = rtm.UntracedRequirements,
            TraceabilityPercentage = rtm.TraceabilityPercentage,
            Requirements = rtm.Requirements.Select(r => new RequirementTraceabilityEntryResponse
            {
                RequirementNumber = r.RequirementNumber,
                Title = r.Title,
                Status = r.Status.ToString(),
                TraceabilityStatus = r.TraceabilityStatus.ToString(),
                DesignElementCount = r.DesignElements.Count,
                CodeImplementationCount = r.CodeImplementations.Count,
                TestCaseCount = r.TestCases.Count
            }).ToList()
        });
    }

    /// <summary>
    /// Verify traceability compliance
    /// </summary>
    [HttpGet("verify")]
    [ProducesResponseType(typeof(TraceabilityVerificationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyTraceability()
    {
        var report = await _rts.VerifyTraceabilityAsync();
        return Ok(new TraceabilityVerificationResponse
        {
            VerifiedAt = report.VerifiedAt,
            TotalRequirements = report.TotalRequirements,
            IssuesFound = report.IssuesFound,
            CriticalIssues = report.CriticalIssues,
            IsCompliant = report.IsCompliant,
            Issues = report.Issues.Select(i => new TraceabilityIssueResponse
            {
                RequirementNumber = i.RequirementNumber,
                IssueType = i.IssueType.ToString(),
                Severity = i.Severity.ToString(),
                Description = i.Description
            }).ToList()
        });
    }
}

// Request/Response Models
public class CreateRequirementRequest
{
    public string RequirementNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
}

public class LinkCodeRequest
{
    public string CodeFile { get; set; } = string.Empty;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string FunctionName { get; set; } = string.Empty;
}

public class LinkTestRequest
{
    public string TestCaseId { get; set; } = string.Empty;
    public string TestFile { get; set; } = string.Empty;
    public string CoverageType { get; set; } = "Statement";
}

public class RequirementResponse
{
    public Guid Id { get; set; }
    public string RequirementNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TraceabilityStatus { get; set; } = string.Empty;
    public List<DesignLinkResponse> DesignLinks { get; set; } = new();
    public List<CodeLinkResponse> CodeLinks { get; set; } = new();
    public List<TestLinkResponse> TestLinks { get; set; } = new();
}

public class DesignLinkResponse
{
    public string DesignElementId { get; set; } = string.Empty;
    public string DesignDocument { get; set; } = string.Empty;
    public bool Verified { get; set; }
}

public class CodeLinkResponse
{
    public string CodeFile { get; set; } = string.Empty;
    public string LineRange { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public bool Verified { get; set; }
}

public class TestLinkResponse
{
    public string TestCaseId { get; set; } = string.Empty;
    public string TestFile { get; set; } = string.Empty;
    public string CoverageType { get; set; } = string.Empty;
    public string TestResult { get; set; } = string.Empty;
    public bool Verified { get; set; }
}

public class RequirementsTraceabilityMatrixResponse
{
    public DateTime GeneratedAt { get; set; }
    public int TotalRequirements { get; set; }
    public int FullyTracedRequirements { get; set; }
    public int PartiallyTracedRequirements { get; set; }
    public int UntracedRequirements { get; set; }
    public double TraceabilityPercentage { get; set; }
    public List<RequirementTraceabilityEntryResponse> Requirements { get; set; } = new();
}

public class RequirementTraceabilityEntryResponse
{
    public string RequirementNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TraceabilityStatus { get; set; } = string.Empty;
    public int DesignElementCount { get; set; }
    public int CodeImplementationCount { get; set; }
    public int TestCaseCount { get; set; }
}

public class TraceabilityVerificationResponse
{
    public DateTime VerifiedAt { get; set; }
    public int TotalRequirements { get; set; }
    public int IssuesFound { get; set; }
    public int CriticalIssues { get; set; }
    public bool IsCompliant { get; set; }
    public List<TraceabilityIssueResponse> Issues { get; set; } = new();
}

public class TraceabilityIssueResponse
{
    public string RequirementNumber { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
