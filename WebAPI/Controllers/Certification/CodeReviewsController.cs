using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;

namespace HB_NLP_Research_Lab.WebAPI.Controllers.Certification;

/// <summary>
/// Code Review Controller for Flight Software Certification
/// DO-178C Level A / NASA NPR 7150.2 Class A
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/certification/code-reviews")]
[ApiVersion("1.0")]
[Authorize(Roles = "Admin")]
[Tags("Certification - Code Reviews")]
public class CodeReviewsController : ControllerBase
{
    private readonly FormalCodeReviewSystem _crs;
    private readonly CodeReviewDbContext _context;
    private readonly ILogger<CodeReviewsController> _logger;

    public CodeReviewsController(
        FormalCodeReviewSystem crs,
        CodeReviewDbContext context,
        ILogger<CodeReviewsController> logger)
    {
        _crs = crs;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a formal code review
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CodeReviewResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReview([FromBody] CreateCodeReviewRequest request)
    {
        var review = new CodeReview
        {
            FilePath = request.FilePath,
            FunctionName = request.FunctionName,
            LineStart = request.LineStart,
            LineEnd = request.LineEnd,
            Author = User.Identity?.Name ?? "System"
        };

        var created = await _crs.CreateReviewAsync(review);

        return CreatedAtAction(nameof(GetReview), new { id = created.Id },
            new CodeReviewResponse
            {
                ReviewNumber = created.ReviewNumber,
                FilePath = created.FilePath,
                FunctionName = created.FunctionName,
                Status = created.Status.ToString(),
                Author = created.Author,
                CreatedAt = created.CreatedAt
            });
    }

    /// <summary>
    /// Get code review by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CodeReviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReview(Guid id)
    {
        var review = await _context.CodeReviews
            .Include(r => r.Assignments)
            .Include(r => r.Findings)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();

        return Ok(new CodeReviewResponse
        {
            ReviewNumber = review.ReviewNumber,
            FilePath = review.FilePath,
            FunctionName = review.FunctionName,
            Status = review.Status.ToString(),
            Author = review.Author,
            ApprovedBy = review.ApprovedBy,
            CreatedAt = review.CreatedAt,
            Findings = review.Findings.Select(f => new ReviewFindingResponse
            {
                Id = f.Id,
                LineNumber = f.LineNumber,
                Severity = f.Severity.ToString(),
                Category = f.Category.ToString(),
                Description = f.Description,
                Recommendation = f.Recommendation,
                Resolved = f.Resolved,
                ResolvedBy = f.ResolvedBy,
                Resolution = f.Resolution
            }).ToList()
        });
    }

    /// <summary>
    /// Register a certified reviewer on the server-owned roster
    /// </summary>
    [HttpPost("certified-reviewers")]
    [ProducesResponseType(typeof(CertifiedReviewerResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterCertifiedReviewer([FromBody] RegisterCertifiedReviewerRequest request)
    {
        try
        {
            var reviewer = await _crs.RegisterCertifiedReviewerAsync(
                request.ReviewerName,
                User.Identity?.Name);
            return CreatedAtAction(
                nameof(ListCertifiedReviewers),
                routeValues: null,
                value: new CertifiedReviewerResponse
                {
                    ReviewerName = reviewer.ReviewerName,
                    IsActive = reviewer.IsActive,
                    CertifiedBy = reviewer.CertifiedBy,
                    CertifiedAt = reviewer.CertifiedAt
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// List certified reviewers
    /// </summary>
    [HttpGet("certified-reviewers")]
    [ProducesResponseType(typeof(IEnumerable<CertifiedReviewerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCertifiedReviewers()
    {
        var reviewers = await _context.CertifiedReviewers
            .AsNoTracking()
            .OrderBy(r => r.ReviewerName)
            .Select(r => new CertifiedReviewerResponse
            {
                ReviewerName = r.ReviewerName,
                IsActive = r.IsActive,
                CertifiedBy = r.CertifiedBy,
                CertifiedAt = r.CertifiedAt
            })
            .ToListAsync();
        return Ok(reviewers);
    }

    /// <summary>
    /// Revoke a certified reviewer
    /// </summary>
    [HttpDelete("certified-reviewers/{reviewerName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeCertifiedReviewer(string reviewerName)
    {
        try
        {
            await _crs.RevokeCertifiedReviewerAsync(reviewerName);
            return Ok(new { message = "Certified reviewer revoked" });
        }
        catch (ArgumentException ex) when (ex.ParamName == "reviewerName")
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Assign a server-roster certified reviewer.
    /// Client-asserted IsCertified flags are ignored — certification comes from the roster only.
    /// </summary>
    [HttpPost("{id}/assign-reviewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignReviewer(Guid id, [FromBody] AssignReviewerRequest request)
    {
        try
        {
            await _crs.AssignReviewerAsync(id, request.ReviewerName);
            return Ok(new { message = "Reviewer assigned successfully" });
        }
        catch (ArgumentException ex) when (ex.ParamName == "reviewerName")
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Submit review findings
    /// </summary>
    [HttpPost("{id}/findings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitFindings(Guid id, [FromBody] SubmitFindingsRequest request)
    {
        try
        {
            var findings = request.Findings.Select(f => new ReviewFinding
            {
                LineNumber = f.LineNumber,
                Severity = Enum.Parse<FindingSeverity>(f.Severity),
                Category = Enum.Parse<FindingCategory>(f.Category),
                Description = f.Description,
                Recommendation = f.Recommendation
            }).ToList();

            await _crs.SubmitFindingsAsync(id, User.Identity?.Name ?? "System", findings);
            return Ok(new { message = "Findings submitted successfully" });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Resolve (disposition) a review finding so it no longer blocks approval.
    /// Requires substantive resolution notes — a bare resolve is not Level A evidence.
    /// </summary>
    [HttpPost("{id}/findings/{findingId}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveFinding(
        Guid id,
        Guid findingId,
        [FromBody] ResolveFindingRequest request)
    {
        try
        {
            if (request is null)
            {
                return BadRequest(new { message = "Finding resolution requires substantive notes" });
            }

            await _crs.ResolveFindingAsync(
                id,
                findingId,
                User.Identity?.Name ?? "System",
                request.Resolution);
            return Ok(new { message = "Finding resolved successfully" });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Approve code review
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveReview(Guid id)
    {
        try
        {
            await _crs.ApproveReviewAsync(id, User.Identity?.Name ?? "System");
            return Ok(new { message = "Code review approved successfully" });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get code review summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(CodeReviewSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _crs.GenerateSummaryAsync();
        return Ok(new CodeReviewSummaryResponse
        {
            GeneratedAt = summary.GeneratedAt,
            TotalReviews = summary.TotalReviews,
            PendingReviews = summary.PendingReviews,
            InProgress = summary.InProgress,
            Completed = summary.Completed,
            Approved = summary.Approved,
            TotalFindings = summary.TotalFindings,
            CriticalFindings = summary.CriticalFindings,
            MajorFindings = summary.MajorFindings,
            MinorFindings = summary.MinorFindings
        });
    }

    /// <summary>
    /// Verify code review compliance
    /// </summary>
    [HttpPost("verify-compliance")]
    [ProducesResponseType(typeof(CodeReviewComplianceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyCompliance([FromBody] VerifyComplianceRequest request)
    {
        var check = await _crs.VerifyComplianceAsync(request.RequiredFiles);
        return Ok(new CodeReviewComplianceResponse
        {
            CheckedAt = check.CheckedAt,
            TotalRequiredFiles = check.TotalRequiredFiles,
            ReviewedFiles = check.ReviewedFiles,
            UnreviewedFiles = check.UnreviewedFiles,
            IsCompliant = check.IsCompliant,
            Issues = check.Issues
        });
    }
}

// Request/Response Models
public class CreateCodeReviewRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
}

public class AssignReviewerRequest
{
    public string ReviewerName { get; set; } = string.Empty;

    /// <summary>
    /// Obsolete client-asserted flag. Ignored by the server; certification is roster-derived.
    /// Kept only so older clients do not fail model binding.
    /// </summary>
    public bool IsCertified { get; set; }
}

public class RegisterCertifiedReviewerRequest
{
    public string ReviewerName { get; set; } = string.Empty;
}

public class CertifiedReviewerResponse
{
    public string ReviewerName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? CertifiedBy { get; set; }
    public DateTime CertifiedAt { get; set; }
}

public class SubmitFindingsRequest
{
    public List<ReviewFindingRequest> Findings { get; set; } = new();
}

public class ResolveFindingRequest
{
    public string Resolution { get; set; } = string.Empty;
}

public class ReviewFindingRequest
{
    public int LineNumber { get; set; }
    public string Severity { get; set; } = "Minor";
    public string Category { get; set; } = "Standards";
    public string Description { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
}

public class VerifyComplianceRequest
{
    public List<string> RequiredFiles { get; set; } = new();
}

public class CodeReviewResponse
{
    public string ReviewNumber { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReviewFindingResponse> Findings { get; set; } = new();
}

public class ReviewFindingResponse
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
    public bool Resolved { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Resolution { get; set; }
}

public class CodeReviewSummaryResponse
{
    public DateTime GeneratedAt { get; set; }
    public int TotalReviews { get; set; }
    public int PendingReviews { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Approved { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int MajorFindings { get; set; }
    public int MinorFindings { get; set; }
}

public class CodeReviewComplianceResponse
{
    public DateTime CheckedAt { get; set; }
    public int TotalRequiredFiles { get; set; }
    public int ReviewedFiles { get; set; }
    public List<string> UnreviewedFiles { get; set; } = new();
    public bool IsCompliant { get; set; }
    public List<string> Issues { get; set; } = new();
}
