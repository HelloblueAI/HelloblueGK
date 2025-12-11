using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;

namespace HB_NLP_Research_Lab.WebAPI.Controllers.Certification;

/// <summary>
/// Test Coverage Controller for Flight Software Certification
/// DO-178C Level A - 100% Coverage + MC/DC
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/certification/test-coverage")]
[ApiVersion("1.0")]
[Authorize]
[Tags("Certification - Test Coverage")]
public class TestCoverageController : ControllerBase
{
    private readonly TestCoverageSystem _tcs;
    private readonly TestCoverageDbContext _context;
    private readonly ILogger<TestCoverageController> _logger;

    public TestCoverageController(
        TestCoverageSystem tcs,
        TestCoverageDbContext context,
        ILogger<TestCoverageController> logger)
    {
        _tcs = tcs;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Record code coverage for a file
    /// </summary>
    [HttpPost("record")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordCoverage([FromBody] RecordCoverageRequest request)
    {
        var metrics = new CoverageMetrics
        {
            StatementCoverage = request.StatementCoverage,
            BranchCoverage = request.BranchCoverage,
            ConditionCoverage = request.ConditionCoverage,
            MCDCCoverage = request.MCDCCoverage,
            PathCoverage = request.PathCoverage,
            TotalStatements = request.TotalStatements,
            CoveredStatements = request.CoveredStatements,
            TotalBranches = request.TotalBranches,
            CoveredBranches = request.CoveredBranches,
            TotalConditions = request.TotalConditions,
            CoveredConditions = request.CoveredConditions
        };

        await _tcs.RecordCoverageAsync(request.FilePath, metrics);
        return Ok(new { message = "Coverage recorded successfully" });
    }

    /// <summary>
    /// Mark file as safety-critical
    /// </summary>
    [HttpPut("{filePath}/safety-critical")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsSafetyCritical(string filePath, [FromBody] SafetyCriticalRequest request)
    {
        await _tcs.MarkAsSafetyCriticalAsync(filePath, request.IsSafetyCritical);
        return Ok(new { message = "File safety-critical status updated" });
    }

    /// <summary>
    /// Get coverage report
    /// </summary>
    [HttpGet("report")]
    [ProducesResponseType(typeof(CoverageReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoverageReport()
    {
        var report = await _tcs.GenerateCoverageReportAsync();
        return Ok(new CoverageReportResponse
        {
            GeneratedAt = report.GeneratedAt,
            TotalFiles = report.TotalFiles,
            FilesWith100PercentStatementCoverage = report.FilesWith100PercentStatementCoverage,
            FilesWith100PercentBranchCoverage = report.FilesWith100PercentBranchCoverage,
            SafetyCriticalFiles = report.SafetyCriticalFiles,
            SafetyCriticalFilesWithMCDC = report.SafetyCriticalFilesWithMCDC,
            OverallStatementCoverage = report.OverallStatementCoverage,
            OverallBranchCoverage = report.OverallBranchCoverage,
            OverallMCDCCoverage = report.OverallMCDCCoverage,
            MeetsDO178CLevelA = report.MeetsDO178CLevelA,
            CoverageGaps = report.CoverageGaps.Select(g => new CoverageGapResponse
            {
                FilePath = g.FilePath,
                StatementCoverage = g.StatementCoverage,
                BranchCoverage = g.BranchCoverage,
                MCDCCoverage = g.MCDCCoverage,
                IsSafetyCritical = g.IsSafetyCritical,
                GapDescription = g.GapDescription
            }).ToList()
        });
    }

    /// <summary>
    /// Verify coverage compliance
    /// </summary>
    [HttpGet("verify-compliance")]
    [ProducesResponseType(typeof(CoverageComplianceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyCompliance()
    {
        var check = await _tcs.VerifyComplianceAsync();
        return Ok(new CoverageComplianceResponse
        {
            CheckedAt = check.CheckedAt,
            TotalFiles = check.TotalFiles,
            FilesWith100PercentStatementCoverage = check.FilesWith100PercentStatementCoverage,
            FilesWith100PercentBranchCoverage = check.FilesWith100PercentBranchCoverage,
            SafetyCriticalFiles = check.SafetyCriticalFiles,
            SafetyCriticalFilesWithMCDC = check.SafetyCriticalFilesWithMCDC,
            StatementCoverageCompliant = check.StatementCoverageCompliant,
            BranchCoverageCompliant = check.BranchCoverageCompliant,
            MCDCCoverageCompliant = check.MCDCCoverageCompliant,
            IsCompliant = check.IsCompliant,
            Issues = check.Issues
        });
    }
}

// Request/Response Models
public class RecordCoverageRequest
{
    public string FilePath { get; set; } = string.Empty;
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

public class SafetyCriticalRequest
{
    public bool IsSafetyCritical { get; set; }
}

public class CoverageReportResponse
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
    public List<CoverageGapResponse> CoverageGaps { get; set; } = new();
}

public class CoverageGapResponse
{
    public string FilePath { get; set; } = string.Empty;
    public double StatementCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double MCDCCoverage { get; set; }
    public bool IsSafetyCritical { get; set; }
    public string GapDescription { get; set; } = string.Empty;
}

public class CoverageComplianceResponse
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
