using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

            _logger.LogInformation("Created software baseline {BaselineName} v{Version}", baselineName, version);
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

            baseline.Status = BaselineStatus.Approved;
            baseline.ApprovedBy = approvedBy;
            baseline.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Approved baseline {BaselineName}", baseline.BaselineName);
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
            // Generate CR number
            var year = DateTime.UtcNow.Year;
            var existingCount = await _context.ChangeRequests
                .CountAsync(cr => cr.RequestNumber.StartsWith($"CR-{year}-"));
            
            request.RequestNumber = $"CR-{year}-{(existingCount + 1):D4}";
            request.Id = Guid.NewGuid();
            request.CreatedAt = DateTime.UtcNow;
            request.Status = ChangeRequestStatus.Submitted;

            _context.ChangeRequests.Add(request);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created change request {RequestNumber}", request.RequestNumber);
            return request;
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

            _logger.LogInformation("Approved change request {RequestNumber}", requestNumber);
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
            report.IsCompliant = report.Issues.Count == 0;

            return report;
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
        MissingBaseline
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
