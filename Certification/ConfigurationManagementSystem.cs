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
    /// Configuration Management System for DO-178C Level A / NASA NPR 7150.2 Class A
    /// Manages software baselines, changes, and configuration items
    /// </summary>
    public class ConfigurationManagementSystem
    {
        private readonly ConfigurationDbContext _context;
        private readonly ILogger<ConfigurationManagementSystem> _logger;

        public ConfigurationManagementSystem(ConfigurationDbContext context, ILogger<ConfigurationManagementSystem> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new software baseline
        /// </summary>
        public async Task<SoftwareBaseline> CreateBaselineAsync(string baselineName, string version, string description, string createdBy)
        {
            var baseline = new SoftwareBaseline
            {
                Id = Guid.NewGuid(),
                BaselineName = baselineName,
                Version = version,
                Description = description,
                Status = BaselineStatus.Draft,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.SoftwareBaselines.Add(baseline);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created software baseline {BaselineName} v{Version}", LogSanitizer.Sanitize(baselineName), LogSanitizer.Sanitize(version));
            return baseline;
        }

        /// <summary>
        /// Approve baseline (makes it official)
        /// </summary>
        public async Task ApproveBaselineAsync(Guid baselineId, string approvedBy)
        {
            var baseline = await _context.SoftwareBaselines.FindAsync(baselineId);
            if (baseline == null)
                throw new ArgumentException($"Baseline {baselineId} not found");

            if (baseline.Status is not (BaselineStatus.Draft or BaselineStatus.UnderReview))
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} cannot be approved from status {baseline.Status}; " +
                    "only Draft or UnderReview baselines may be approved");
            }

            baseline.Status = BaselineStatus.Approved;
            baseline.ApprovedBy = approvedBy;
            baseline.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Approved baseline {BaselineName}", LogSanitizer.Sanitize(baseline.BaselineName));
        }

        /// <summary>
        /// Create a configuration item (file, document, etc.)
        /// </summary>
        public async Task<ConfigurationItem> CreateConfigurationItemAsync(ConfigurationItem item)
        {
            item.Id = Guid.NewGuid();
            item.CreatedAt = DateTime.UtcNow;
            item.Status = ConfigurationItemStatus.UnderDevelopment;

            _context.ConfigurationItems.Add(item);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created configuration item {ItemName}", item.ItemName);
            return item;
        }

        /// <summary>
        /// Add configuration item to baseline
        /// </summary>
        public async Task AddItemToBaselineAsync(Guid baselineId, Guid itemId, string version)
        {
            var baseline = await _context.SoftwareBaselines.FindAsync(baselineId);
            if (baseline == null)
                throw new ArgumentException($"Baseline {baselineId} not found");

            // Approved/Released/Obsolete baselines are frozen; mutations require a change request path.
            if (baseline.Status is BaselineStatus.Approved or BaselineStatus.Released or BaselineStatus.Obsolete)
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} is {baseline.Status} and cannot accept new configuration items");
            }

            var baselineItem = new BaselineConfigurationItem
            {
                Id = Guid.NewGuid(),
                BaselineId = baselineId,
                ConfigurationItemId = itemId,
                Version = version,
                AddedAt = DateTime.UtcNow
            };

            _context.BaselineConfigurationItems.Add(baselineItem);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added configuration item {ItemId} to baseline {BaselineId}", itemId, baselineId);
        }

        /// <summary>
        /// Create a change request
        /// </summary>
        public async Task<ChangeRequest> CreateChangeRequestAsync(ChangeRequest request)
        {
            request.Id = Guid.NewGuid();
            request.CreatedAt = DateTime.UtcNow;
            request.Status = ChangeRequestStatus.Submitted;

            const int maxAttempts = 8;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                request.RequestNumber = await AllocateNextRequestNumberAsync();
                _context.ChangeRequests.Add(request);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Created change request {RequestNumber}", request.RequestNumber);
                    return request;
                }
                catch (DbUpdateException) when (attempt < maxAttempts - 1)
                {
                    // Unique RequestNumber race — detach and retry with a fresh sequence value.
                    _context.Entry(request).State = EntityState.Detached;
                }
            }

            throw new InvalidOperationException("Unable to allocate a unique change request number");
        }

        /// <summary>
        /// Approve change request (requires CCB approval)
        /// </summary>
        public async Task ApproveChangeRequestAsync(string requestNumber, string approvedBy, string approvalNotes)
        {
            var request = await _context.ChangeRequests
                .FirstOrDefaultAsync(cr => cr.RequestNumber == requestNumber);

            if (request == null)
                throw new ArgumentException($"Change request {requestNumber} not found");

            if (request.Status is not (ChangeRequestStatus.Submitted or ChangeRequestStatus.UnderReview))
            {
                throw new InvalidOperationException(
                    $"Change request {requestNumber} cannot be approved from status {request.Status}; " +
                    "only Submitted or UnderReview change requests may be approved");
            }

            request.Status = ChangeRequestStatus.Approved;
            request.ApprovedBy = approvedBy;
            request.ApprovedAt = DateTime.UtcNow;
            request.ApprovalNotes = approvalNotes;

            // Track approval
            var approval = new ChangeRequestApproval
            {
                Id = Guid.NewGuid(),
                ChangeRequestId = request.Id,
                ApprovedBy = approvedBy,
                ApprovedAt = DateTime.UtcNow,
                Notes = approvalNotes
            };

            _context.ChangeRequestApprovals.Add(approval);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Approved change request {RequestNumber}", LogSanitizer.SanitizeIdentifier(requestNumber));
        }

        /// <summary>
        /// Implement change request
        /// </summary>
        public async Task ImplementChangeRequestAsync(string requestNumber, string implementedBy, List<Guid> affectedItems)
        {
            var request = await _context.ChangeRequests
                .FirstOrDefaultAsync(cr => cr.RequestNumber == requestNumber);

            if (request == null)
                throw new ArgumentException($"Change request {requestNumber} not found");

            if (request.Status != ChangeRequestStatus.Approved)
                throw new InvalidOperationException($"Change request {requestNumber} must be approved before implementation");

            request.Status = ChangeRequestStatus.Implemented;
            request.ImplementedBy = implementedBy;
            request.ImplementedAt = DateTime.UtcNow;

            // Link to affected items
            var links = affectedItems.Select(itemId => new ChangeRequestItemLink
            {
                Id = Guid.NewGuid(),
                ChangeRequestId = request.Id,
                ConfigurationItemId = itemId,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            _context.ChangeRequestItemLinks.AddRange(links);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Implemented change request {RequestNumber}", requestNumber);
        }

        /// <summary>
        /// Generate Software Configuration Index (SCI) - Required for DO-178C Level A
        /// </summary>
        public async Task<SoftwareConfigurationIndex> GenerateSCIAsync(Guid baselineId)
        {
            var baseline = await _context.SoftwareBaselines
                .Include(b => b.ConfigurationItems)
                .ThenInclude(ci => ci.ConfigurationItem)
                .FirstOrDefaultAsync(b => b.Id == baselineId);

            if (baseline == null)
                throw new ArgumentException($"Baseline {baselineId} not found");

            // SCI is certification evidence — only official baselines may produce an index.
            if (baseline.Status is not (BaselineStatus.Approved or BaselineStatus.Released))
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} is {baseline.Status}; SCI may only be generated for Approved or Released baselines");
            }

            var sci = new SoftwareConfigurationIndex
            {
                BaselineId = baselineId,
                BaselineName = baseline.BaselineName,
                Version = baseline.Version,
                GeneratedAt = DateTime.UtcNow,
                ConfigurationItems = baseline.ConfigurationItems.Select(bci => new SCIEntry
                {
                    ItemName = bci.ConfigurationItem.ItemName,
                    ItemType = bci.ConfigurationItem.ItemType,
                    Version = bci.Version,
                    FilePath = bci.ConfigurationItem.FilePath,
                    Checksum = bci.ConfigurationItem.Checksum,
                    Size = bci.ConfigurationItem.Size
                }).ToList()
            };

            return sci;
        }

        /// <summary>
        /// Perform configuration audit
        /// </summary>
        public async Task<ConfigurationAuditReport> PerformAuditAsync(Guid baselineId)
        {
            var baseline = await _context.SoftwareBaselines
                .Include(b => b.ConfigurationItems)
                .ThenInclude(ci => ci.ConfigurationItem)
                .FirstOrDefaultAsync(b => b.Id == baselineId);

            if (baseline == null)
                throw new ArgumentException($"Baseline {baselineId} not found");

            var report = new ConfigurationAuditReport
            {
                BaselineId = baselineId,
                BaselineName = baseline.BaselineName,
                AuditedAt = DateTime.UtcNow,
                Issues = new List<ConfigurationAuditIssue>()
            };

            // Check for missing items
            var items = baseline.ConfigurationItems.Select(bci => bci.ConfigurationItem).ToList();
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Checksum))
                {
                    report.Issues.Add(new ConfigurationAuditIssue
                    {
                        ItemName = item.ItemName,
                        IssueType = AuditIssueType.MissingChecksum,
                        Severity = IssueSeverity.Major,
                        Description = $"Configuration item {item.ItemName} has no checksum"
                    });
                }

                if (item.Status != ConfigurationItemStatus.Released)
                {
                    report.Issues.Add(new ConfigurationAuditIssue
                    {
                        ItemName = item.ItemName,
                        IssueType = AuditIssueType.ItemNotReleased,
                        Severity = IssueSeverity.Major,
                        Description = $"Configuration item {item.ItemName} is not in Released status"
                    });
                }
            }

            report.TotalItems = items.Count;
            report.IssuesFound = report.Issues.Count;

            // Empty baselines must fail closed — 0 issues on 0 items is not DO-178C evidence.
            if (report.TotalItems == 0)
            {
                report.IsCompliant = false;
                report.Issues.Add(new ConfigurationAuditIssue
                {
                    ItemName = baseline.BaselineName,
                    IssueType = AuditIssueType.MissingBaseline,
                    Severity = IssueSeverity.Critical,
                    Description = $"Baseline {baseline.BaselineName} has no configuration items; compliance cannot be asserted"
                });
                report.IssuesFound = report.Issues.Count;
                return report;
            }

            // Draft/UnderReview/Obsolete baselines must not report compliance even when items look clean.
            if (baseline.Status is not (BaselineStatus.Approved or BaselineStatus.Released))
            {
                report.IsCompliant = false;
                report.Issues.Add(new ConfigurationAuditIssue
                {
                    ItemName = baseline.BaselineName,
                    IssueType = AuditIssueType.BaselineNotApproved,
                    Severity = IssueSeverity.Critical,
                    Description =
                        $"Baseline {baseline.BaselineName} is {baseline.Status}; configuration compliance requires an Approved or Released baseline"
                });
                report.IssuesFound = report.Issues.Count;
                return report;
            }

            report.IsCompliant = report.Issues.Count == 0;

            return report;
        }

        private async Task<string> AllocateNextRequestNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"CR-{year}-";
            var lastNumber = await _context.ChangeRequests
                .Where(cr => cr.RequestNumber.StartsWith(prefix))
                .OrderByDescending(cr => cr.RequestNumber)
                .Select(cr => cr.RequestNumber)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrEmpty(lastNumber)
                && lastNumber.Length > prefix.Length
                && int.TryParse(lastNumber.AsSpan(prefix.Length), out var parsed)
                && parsed >= 0)
            {
                next = parsed + 1;
            }

            return $"{prefix}{next:D4}";
        }
    }

    // Data Models
    public class SoftwareBaseline
    {
        public Guid Id { get; set; }
        public string BaselineName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BaselineStatus Status { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public List<BaselineConfigurationItem> ConfigurationItems { get; set; } = new();
    }

    public class ConfigurationItem
    {
        public Guid Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public ConfigurationItemType ItemType { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? Checksum { get; set; }
        public long? Size { get; set; }
        public ConfigurationItemStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
    }

    public class BaselineConfigurationItem
    {
        public Guid Id { get; set; }
        public Guid BaselineId { get; set; }
        public Guid ConfigurationItemId { get; set; }
        public string Version { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }

        public SoftwareBaseline Baseline { get; set; } = null!;
        public ConfigurationItem ConfigurationItem { get; set; } = null!;
    }

    public class ChangeRequest
    {
        public Guid Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public ChangeRequestStatus Status { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNotes { get; set; }
        public string? ImplementedBy { get; set; }
        public DateTime? ImplementedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<ChangeRequestApproval> Approvals { get; set; } = new();
        public List<ChangeRequestItemLink> AffectedItems { get; set; } = new();
    }

    public class ChangeRequestApproval
    {
        public Guid Id { get; set; }
        public Guid ChangeRequestId { get; set; }
        public string ApprovedBy { get; set; } = string.Empty;
        public DateTime ApprovedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class ChangeRequestItemLink
    {
        public Guid Id { get; set; }
        public Guid ChangeRequestId { get; set; }
        public Guid ConfigurationItemId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public enum BaselineStatus
    {
        Draft,
        UnderReview,
        Approved,
        Released,
        Obsolete
    }

    public enum ConfigurationItemType
    {
        SourceCode,
        HeaderFile,
        TestCode,
        Documentation,
        ConfigurationFile,
        BuildScript,
        Tool,
        DataFile
    }

    public enum ConfigurationItemStatus
    {
        UnderDevelopment,
        UnderReview,
        Approved,
        Released,
        Obsolete
    }

    public enum ChangeRequestStatus
    {
        Submitted,
        UnderReview,
        Approved,
        Rejected,
        Implemented,
        Verified,
        Closed
    }

    public class SoftwareConfigurationIndex
    {
        public Guid BaselineId { get; set; }
        public string BaselineName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public List<SCIEntry> ConfigurationItems { get; set; } = new();
    }

    public class SCIEntry
    {
        public string ItemName { get; set; } = string.Empty;
        public ConfigurationItemType ItemType { get; set; }
        public string Version { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? Checksum { get; set; }
        public long? Size { get; set; }
    }

    public class ConfigurationAuditReport
    {
        public Guid BaselineId { get; set; }
        public string BaselineName { get; set; } = string.Empty;
        public DateTime AuditedAt { get; set; }
        public int TotalItems { get; set; }
        public int IssuesFound { get; set; }
        public bool IsCompliant { get; set; }
        public List<ConfigurationAuditIssue> Issues { get; set; } = new();
    }

    public class ConfigurationAuditIssue
    {
        public string ItemName { get; set; } = string.Empty;
        public AuditIssueType IssueType { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public enum AuditIssueType
    {
        MissingChecksum,
        ItemNotReleased,
        MissingVersion,
        InvalidChecksum,
        MissingBaseline,
        BaselineNotApproved
    }

    // DbContext
    public class ConfigurationDbContext : DbContext
    {
        public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : base(options) { }

        public DbSet<SoftwareBaseline> SoftwareBaselines { get; set; }
        public DbSet<ConfigurationItem> ConfigurationItems { get; set; }
        public DbSet<BaselineConfigurationItem> BaselineConfigurationItems { get; set; }
        public DbSet<ChangeRequest> ChangeRequests { get; set; }
        public DbSet<ChangeRequestApproval> ChangeRequestApprovals { get; set; }
        public DbSet<ChangeRequestItemLink> ChangeRequestItemLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BaselineConfigurationItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Baseline).WithMany(b => b.ConfigurationItems).HasForeignKey(e => e.BaselineId);
                entity.HasOne(e => e.ConfigurationItem).WithMany().HasForeignKey(e => e.ConfigurationItemId);
            });

            modelBuilder.Entity<ChangeRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RequestNumber).IsUnique();
            });
        }
    }
}
