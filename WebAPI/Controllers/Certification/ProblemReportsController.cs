using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;
using HB_NLP_Research_Lab.WebAPI.Models;

namespace HB_NLP_Research_Lab.WebAPI.Controllers.Certification;

/// <summary>
/// Problem Reporting Controller for Flight Software Certification
/// DO-178C Level A / NASA NPR 7150.2 Class A
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/certification/problem-reports")]
[ApiVersion("1.0")]
[Authorize]
[Tags("Certification - Problem Reports")]
public class ProblemReportsController : ControllerBase
{
    private readonly ProblemReportingSystem _prs;
    private readonly ProblemReportDbContext _context;
    private readonly ILogger<ProblemReportsController> _logger;

    public ProblemReportsController(
        ProblemReportingSystem prs,
        ProblemReportDbContext context,
        ILogger<ProblemReportsController> logger)
    {
        _prs = prs;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a new problem report
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProblemReportResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProblemReport([FromBody] CreateProblemReportRequest request)
    {
        var report = new ProblemReport
        {
            Title = request.Title,
            Description = request.Description,
            Impact = request.Impact,
            ReportedBy = User.Identity?.Name ?? "System"
        };

        var created = await _prs.CreateProblemReportAsync(report);

        return CreatedAtAction(nameof(GetProblemReport), new { reportNumber = created.ReportNumber },
            new ProblemReportResponse
            {
                ReportNumber = created.ReportNumber,
                Title = created.Title,
                Description = created.Description,
                Impact = created.Impact,
                Severity = created.Severity.ToString(),
                Status = created.Status.ToString(),
                ReportedBy = created.ReportedBy,
                CreatedAt = created.CreatedAt
            });
    }

    /// <summary>
    /// Get problem report by report number
    /// </summary>
    [HttpGet("{reportNumber}")]
    [ProducesResponseType(typeof(ProblemReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProblemReport(string reportNumber)
    {
        var report = await _context.ProblemReports
            .FirstOrDefaultAsync(pr => pr.ReportNumber == reportNumber);

        if (report == null)
            return NotFound();

        return Ok(new ProblemReportResponse
        {
            ReportNumber = report.ReportNumber,
            Title = report.Title,
            Description = report.Description,
            Impact = report.Impact,
            Severity = report.Severity.ToString(),
            Status = report.Status.ToString(),
            ReportedBy = report.ReportedBy,
            AssignedTo = report.AssignedTo,
            Resolution = report.Resolution,
            CreatedAt = report.CreatedAt,
            ClosedAt = report.ClosedAt
        });
    }

    /// <summary>
    /// Update problem report status
    /// </summary>
    [HttpPut("{reportNumber}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(string reportNumber, [FromBody] UpdateProblemReportStatusRequest request)
    {
        try
        {
            var status = Enum.Parse<ProblemReportStatus>(request.Status);
            await _prs.UpdateStatusAsync(reportNumber, status, request.Resolution);
            return Ok(new { message = "Problem report status updated successfully" });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Get problem report summary
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ProblemReportSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _prs.GenerateSummaryAsync();
        return Ok(new ProblemReportSummaryResponse
        {
            GeneratedAt = summary.GeneratedAt,
            TotalReports = summary.TotalReports,
            OpenReports = summary.OpenReports,
            UnderInvestigation = summary.UnderInvestigation,
            Resolved = summary.Resolved,
            Closed = summary.Closed,
            CriticalSeverity = summary.CriticalSeverity,
            MajorSeverity = summary.MajorSeverity,
            MinorSeverity = summary.MinorSeverity,
            AverageResolutionTime = summary.AverageResolutionTime
        });
    }

    /// <summary>
    /// Verify problem report compliance
    /// </summary>
    [HttpGet("verify-compliance")]
    [ProducesResponseType(typeof(ProblemReportComplianceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyCompliance()
    {
        var check = await _prs.VerifyComplianceAsync();
        return Ok(new ProblemReportComplianceResponse
        {
            CheckedAt = check.CheckedAt,
            TotalCriticalProblems = check.TotalCriticalProblems,
            UnresolvedCriticalProblems = check.UnresolvedCriticalProblems,
            TotalMajorProblems = check.TotalMajorProblems,
            UnresolvedMajorProblems = check.UnresolvedMajorProblems,
            IsCompliant = check.IsCompliant,
            Issues = check.Issues
        });
    }
}

// Request/Response Models
public class CreateProblemReportRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}

public class UpdateProblemReportStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Resolution { get; set; }
}

public class ProblemReportResponse
{
    public string ReportNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class ProblemReportSummaryResponse
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
}

public class ProblemReportComplianceResponse
{
    public DateTime CheckedAt { get; set; }
    public int TotalCriticalProblems { get; set; }
    public int UnresolvedCriticalProblems { get; set; }
    public int TotalMajorProblems { get; set; }
    public int UnresolvedMajorProblems { get; set; }
    public bool IsCompliant { get; set; }
    public List<string> Issues { get; set; } = new();
}
