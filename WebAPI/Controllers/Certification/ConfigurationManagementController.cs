using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HB_NLP_Research_Lab.Certification;
using HB_NLP_Research_Lab.WebAPI.Data;

namespace HB_NLP_Research_Lab.WebAPI.Controllers.Certification;

/// <summary>
/// Configuration Management Controller for Flight Software Certification
/// DO-178C Level A / NASA NPR 7150.2 Class A
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/certification/configuration")]
[ApiVersion("1.0")]
[Authorize]
[Tags("Certification - Configuration Management")]
public class ConfigurationManagementController : ControllerBase
{
    private readonly ConfigurationManagementSystem _cms;
    private readonly ConfigurationDbContext _context;
    private readonly ILogger<ConfigurationManagementController> _logger;

    public ConfigurationManagementController(
        ConfigurationManagementSystem cms,
        ConfigurationDbContext context,
        ILogger<ConfigurationManagementController> logger)
    {
        _cms = cms;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a software baseline
    /// </summary>
    [HttpPost("baselines")]
    [ProducesResponseType(typeof(BaselineResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBaseline([FromBody] CreateBaselineRequest request)
    {
        var baseline = await _cms.CreateBaselineAsync(
            request.BaselineName,
            request.Version,
            request.Description,
            User.Identity?.Name ?? "System");

        return CreatedAtAction(nameof(GetBaseline), new { id = baseline.Id },
            new BaselineResponse
            {
                Id = baseline.Id,
                BaselineName = baseline.BaselineName,
                Version = baseline.Version,
                Description = baseline.Description,
                Status = baseline.Status.ToString(),
                CreatedBy = baseline.CreatedBy,
                CreatedAt = baseline.CreatedAt
            });
    }

    /// <summary>
    /// Get baseline by ID
    /// </summary>
    [HttpGet("baselines/{id}")]
    [ProducesResponseType(typeof(BaselineResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBaseline(Guid id)
    {
        var baseline = await _context.SoftwareBaselines.FindAsync(id);
        if (baseline == null)
            return NotFound();

        return Ok(new BaselineResponse
        {
            Id = baseline.Id,
            BaselineName = baseline.BaselineName,
            Version = baseline.Version,
            Description = baseline.Description,
            Status = baseline.Status.ToString(),
            CreatedBy = baseline.CreatedBy,
            CreatedAt = baseline.CreatedAt
        });
    }

    /// <summary>
    /// Approve baseline
    /// </summary>
    [HttpPost("baselines/{id}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveBaseline(Guid id)
    {
        try
        {
            await _cms.ApproveBaselineAsync(id, User.Identity?.Name ?? "System");
            return Ok(new { message = "Baseline approved successfully" });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Create change request
    /// </summary>
    [HttpPost("change-requests")]
    [ProducesResponseType(typeof(ChangeRequestResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateChangeRequest([FromBody] CreateChangeRequestRequest request)
    {
        var changeRequest = new ChangeRequest
        {
            Title = request.Title,
            Description = request.Description,
            Justification = request.Justification,
            RequestedBy = User.Identity?.Name ?? "System"
        };

        var created = await _cms.CreateChangeRequestAsync(changeRequest);

        return CreatedAtAction(nameof(GetChangeRequest), new { requestNumber = created.RequestNumber },
            new ChangeRequestResponse
            {
                RequestNumber = created.RequestNumber,
                Title = created.Title,
                Description = created.Description,
                Status = created.Status.ToString(),
                RequestedBy = created.RequestedBy,
                CreatedAt = created.CreatedAt
            });
    }

    /// <summary>
    /// Get change request by number
    /// </summary>
    [HttpGet("change-requests/{requestNumber}")]
    [ProducesResponseType(typeof(ChangeRequestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeRequest(string requestNumber)
    {
        var request = await _context.ChangeRequests
            .FirstOrDefaultAsync(cr => cr.RequestNumber == requestNumber);

        if (request == null)
            return NotFound();

        return Ok(new ChangeRequestResponse
        {
            RequestNumber = request.RequestNumber,
            Title = request.Title,
            Description = request.Description,
            Status = request.Status.ToString(),
            RequestedBy = request.RequestedBy,
            ApprovedBy = request.ApprovedBy,
            CreatedAt = request.CreatedAt
        });
    }

    /// <summary>
    /// Approve change request
    /// </summary>
    [HttpPost("change-requests/{requestNumber}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveChangeRequest(string requestNumber, [FromBody] ApproveChangeRequestRequest request)
    {
        try
        {
            await _cms.ApproveChangeRequestAsync(requestNumber, User.Identity?.Name ?? "System", request.ApprovalNotes ?? string.Empty);
            return Ok(new { message = "Change request approved successfully" });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Generate Software Configuration Index (SCI)
    /// </summary>
    [HttpGet("baselines/{id}/sci")]
    [ProducesResponseType(typeof(SoftwareConfigurationIndexResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateSCI(Guid id)
    {
        try
        {
            var sci = await _cms.GenerateSCIAsync(id);
            return Ok(new SoftwareConfigurationIndexResponse
            {
                BaselineName = sci.BaselineName,
                Version = sci.Version,
                GeneratedAt = sci.GeneratedAt,
                ConfigurationItems = sci.ConfigurationItems.Select(ci => new SCIEntryResponse
                {
                    ItemName = ci.ItemName,
                    ItemType = ci.ItemType.ToString(),
                    Version = ci.Version,
                    FilePath = ci.FilePath,
                    Checksum = ci.Checksum,
                    Size = ci.Size
                }).ToList()
            });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Perform configuration audit
    /// </summary>
    [HttpGet("baselines/{id}/audit")]
    [ProducesResponseType(typeof(ConfigurationAuditResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PerformAudit(Guid id)
    {
        try
        {
            var audit = await _cms.PerformAuditAsync(id);
            return Ok(new ConfigurationAuditResponse
            {
                BaselineName = audit.BaselineName,
                AuditedAt = audit.AuditedAt,
                TotalItems = audit.TotalItems,
                IssuesFound = audit.IssuesFound,
                IsCompliant = audit.IsCompliant,
                Issues = audit.Issues.Select(i => new ConfigurationAuditIssueResponse
                {
                    ItemName = i.ItemName,
                    IssueType = i.IssueType.ToString(),
                    Severity = i.Severity.ToString(),
                    Description = i.Description
                }).ToList()
            });
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }
}

// Request/Response Models
public class CreateBaselineRequest
{
    public string BaselineName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CreateChangeRequestRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Justification { get; set; } = string.Empty;
}

public class ApproveChangeRequestRequest
{
    public string? ApprovalNotes { get; set; }
}

public class BaselineResponse
{
    public Guid Id { get; set; }
    public string BaselineName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChangeRequestResponse
{
    public string RequestNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SoftwareConfigurationIndexResponse
{
    public string BaselineName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public List<SCIEntryResponse> ConfigurationItems { get; set; } = new();
}

public class SCIEntryResponse
{
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public long? Size { get; set; }
}

public class ConfigurationAuditResponse
{
    public string BaselineName { get; set; } = string.Empty;
    public DateTime AuditedAt { get; set; }
    public int TotalItems { get; set; }
    public int IssuesFound { get; set; }
    public bool IsCompliant { get; set; }
    public List<ConfigurationAuditIssueResponse> Issues { get; set; } = new();
}

public class ConfigurationAuditIssueResponse
{
    public string ItemName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
