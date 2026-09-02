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
            if (string.IsNullOrWhiteSpace(baselineName))
                throw new ArgumentException("Baseline name is required", nameof(baselineName));
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Baseline version is required", nameof(version));

            var baseline = new SoftwareBaseline
            {
                Id = Guid.NewGuid(),
                BaselineName = baselineName.Trim(),
                Version = version.Trim(),
                Description = description,
                Status = BaselineStatus.Draft,
                CreatedBy = NormalizeActorIdentity(createdBy, nameof(createdBy)),
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
            var baseline = await _context.SoftwareBaselines
                .Include(b => b.ConfigurationItems)
                    .ThenInclude(ci => ci.ConfigurationItem)
                .FirstOrDefaultAsync(b => b.Id == baselineId);
            if (baseline == null)
                throw new ArgumentException($"Baseline {baselineId} not found");

            if (baseline.Status is not (BaselineStatus.Draft or BaselineStatus.UnderReview))
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} cannot be approved from status {baseline.Status}; " +
                    "only Draft or UnderReview baselines may be approved");
            }

            // Empty / unreleased / checksum-free items must not become official —
            // Approve freezes the set and SCI would otherwise be vacuous.
            if (baseline.ConfigurationItems == null || baseline.ConfigurationItems.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} has no configuration items and cannot be approved");
            }

            if (!HasReleasedChecksumEvidence(baseline.ConfigurationItems))
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} cannot be approved until every configuration item is Released with a checksum");
            }

            // Level A independence: approver must not be the baseline author.
            // Empty or placeholder creators previously skipped this gate.
            var normalizedApprover = NormalizeActorIdentity(approvedBy, nameof(approvedBy));
            if (!HasRealActorIdentity(baseline.CreatedBy))
            {
                throw new InvalidOperationException(
                    "Cannot approve a baseline without a real creator identity; Level A SoD cannot be evaluated");
            }

            if (string.Equals(NormalizeActorName(baseline.CreatedBy), normalizedApprover, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cannot approve a baseline as its creator; Level A requires an independent approver");
            }

            // Atomic Draft|UnderReview → Approved claim closes load/check/SaveChanges TOCTOU
            // (concurrent empty-item races / double-approve).
            var approvedAt = DateTime.UtcNow;
            var claimed = await _context.SoftwareBaselines
                .Where(b => b.Id == baselineId &&
                            (b.Status == BaselineStatus.Draft || b.Status == BaselineStatus.UnderReview))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.Status, BaselineStatus.Approved)
                    .SetProperty(b => b.ApprovedBy, normalizedApprover)
                    .SetProperty(b => b.ApprovedAt, approvedAt));

            if (claimed == 0)
            {
                await _context.Entry(baseline).ReloadAsync();
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} cannot be approved from status {baseline.Status}; " +
                    "concurrent status change detected");
            }

            // Re-check item evidence after claim — a concurrent empty or unreleased
            // baseline must not stay Approved.
            var claimedItems = await _context.BaselineConfigurationItems
                .Where(i => i.BaselineId == baselineId)
                .Include(i => i.ConfigurationItem)
                .ToListAsync();
            if (claimedItems.Count == 0 || !HasReleasedChecksumEvidence(claimedItems))
            {
                await _context.SoftwareBaselines
                    .Where(b => b.Id == baselineId && b.Status == BaselineStatus.Approved)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.Status, BaselineStatus.Draft)
                        .SetProperty(b => b.ApprovedBy, (string?)null)
                        .SetProperty(b => b.ApprovedAt, (DateTime?)null));

                throw new InvalidOperationException(
                    claimedItems.Count == 0
                        ? $"Baseline {baseline.BaselineName} has no configuration items and cannot be approved"
                        : $"Baseline {baseline.BaselineName} cannot be approved until every configuration item is Released with a checksum");
            }

            baseline.Status = BaselineStatus.Approved;
            baseline.ApprovedBy = normalizedApprover;
            baseline.ApprovedAt = approvedAt;

            _logger.LogInformation("Approved baseline {BaselineName}", LogSanitizer.Sanitize(baseline.BaselineName));
        }

        /// <summary>
        /// Create a configuration item (file, document, etc.)
        /// </summary>
        public async Task<ConfigurationItem> CreateConfigurationItemAsync(ConfigurationItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.ItemName = NormalizeRequiredText(item.ItemName, nameof(item.ItemName));
            item.FilePath = NormalizeEvidencePath(item.FilePath);

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
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Configuration item version is required", nameof(version));

            var baseline = await _context.SoftwareBaselines.FindAsync(baselineId);
            if (baseline == null)
                throw new ArgumentException($"Baseline {baselineId} not found");

            var item = await _context.ConfigurationItems.FindAsync(itemId);
            if (item == null)
                throw new ArgumentException($"Configuration item {itemId} not found", nameof(itemId));

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
                Version = version.Trim(),
                AddedAt = DateTime.UtcNow
            };

            _context.BaselineConfigurationItems.Add(baselineItem);
            await _context.SaveChangesAsync();

            // Post-save recheck closes the race where ApproveBaseline claims Approved after the
            // pre-insert Draft/UnderReview check but before this insert commits — without this,
            // items can land on a frozen Approved baseline.
            await _context.Entry(baseline).ReloadAsync();
            if (baseline.Status is BaselineStatus.Approved or BaselineStatus.Released or BaselineStatus.Obsolete)
            {
                _context.BaselineConfigurationItems.Remove(baselineItem);
                await _context.SaveChangesAsync();
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} is {baseline.Status} and cannot accept new configuration items");
            }

            _logger.LogInformation("Added configuration item {ItemId} to baseline {BaselineId}", itemId, baselineId);
        }

        /// <summary>
        /// Create a change request
        /// </summary>
        public async Task<ChangeRequest> CreateChangeRequestAsync(ChangeRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Change request title is required", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("Change request description is required", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Justification))
                throw new ArgumentException("Change request justification is required", nameof(request));

            request.Title = request.Title.Trim();
            request.Description = request.Description.Trim();
            request.Justification = request.Justification.Trim();
            request.RequestedBy = NormalizeActorIdentity(request.RequestedBy, nameof(request.RequestedBy));
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
            if (string.IsNullOrWhiteSpace(approvalNotes))
                throw new ArgumentException("Approval notes are required for CCB approval", nameof(approvalNotes));

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

            var notes = approvalNotes.Trim();
            if (!HasSubstantiveApprovalNotes(notes))
            {
                throw new ArgumentException(
                    "Approval notes must be substantive; vacuous text such as 'ok'/'lgtm' cannot record CCB approval",
                    nameof(approvalNotes));
            }

            // Level A / CCB independence: requester cannot self-approve.
            // Empty or placeholder requesters previously skipped this gate.
            var normalizedApprover = NormalizeActorIdentity(approvedBy, nameof(approvedBy));
            if (!HasRealActorIdentity(request.RequestedBy))
            {
                throw new InvalidOperationException(
                    "Cannot approve a change request without a real requester identity; Level A SoD cannot be evaluated");
            }

            if (string.Equals(NormalizeActorName(request.RequestedBy), normalizedApprover, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cannot approve a change request as its requester; Level A requires separation of duties");
            }

            // Atomic Submitted|UnderReview → Approved claim closes double-approve TOCTOU.
            var approvedAt = DateTime.UtcNow;
            var claimed = await _context.ChangeRequests
                .Where(cr => cr.Id == request.Id &&
                             (cr.Status == ChangeRequestStatus.Submitted ||
                              cr.Status == ChangeRequestStatus.UnderReview))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(cr => cr.Status, ChangeRequestStatus.Approved)
                    .SetProperty(cr => cr.ApprovedBy, normalizedApprover)
                    .SetProperty(cr => cr.ApprovedAt, approvedAt)
                    .SetProperty(cr => cr.ApprovalNotes, notes));

            if (claimed == 0)
            {
                await _context.Entry(request).ReloadAsync();
                throw new InvalidOperationException(
                    $"Change request {requestNumber} cannot be approved from status {request.Status}; " +
                    "concurrent status change detected");
            }

            _context.ChangeRequestApprovals.Add(new ChangeRequestApproval
            {
                Id = Guid.NewGuid(),
                ChangeRequestId = request.Id,
                ApprovedBy = normalizedApprover,
                ApprovedAt = approvedAt,
                Notes = notes
            });
            await _context.SaveChangesAsync();

            request.Status = ChangeRequestStatus.Approved;
            request.ApprovedBy = normalizedApprover;
            request.ApprovedAt = approvedAt;
            request.ApprovalNotes = notes;

            _logger.LogInformation("Approved change request {RequestNumber}", LogSanitizer.SanitizeIdentifier(requestNumber));
        }

        /// <summary>
        /// Implement change request
        /// </summary>
        public async Task ImplementChangeRequestAsync(string requestNumber, string implementedBy, List<Guid> affectedItems)
        {
            ArgumentNullException.ThrowIfNull(affectedItems);
            if (affectedItems.Count == 0 || affectedItems.Any(id => id == Guid.Empty))
            {
                throw new ArgumentException(
                    "At least one existing affected configuration item is required to implement a change request",
                    nameof(affectedItems));
            }

            var distinctItemIds = affectedItems.Distinct().ToList();
            var existingCount = await _context.ConfigurationItems
                .CountAsync(item => distinctItemIds.Contains(item.Id));
            if (existingCount != distinctItemIds.Count)
            {
                throw new ArgumentException(
                    "One or more affected configuration items were not found",
                    nameof(affectedItems));
            }

            var request = await _context.ChangeRequests
                .FirstOrDefaultAsync(cr => cr.RequestNumber == requestNumber);

            if (request == null)
                throw new ArgumentException($"Change request {requestNumber} not found");

            if (request.Status != ChangeRequestStatus.Approved)
                throw new InvalidOperationException($"Change request {requestNumber} must be approved before implementation");

            var normalizedImplementer = NormalizeActorIdentity(implementedBy, nameof(implementedBy));

            // Level A independence: the CCB approver cannot also implement the change.
            // Requester-as-implementer remains allowed (developer implements after CCB).
            if (!string.IsNullOrWhiteSpace(request.ApprovedBy) &&
                string.Equals(
                    NormalizeActorName(request.ApprovedBy),
                    normalizedImplementer,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Cannot implement a change request as its CCB approver; Level A requires separation of duties");
            }

            // Claim and item links share one transaction so a failure after the
            // Approved → Implemented update cannot leave an Implemented row with
            // no ChangeRequestItemLink evidence (retry would then be rejected).
            var implementedAt = DateTime.UtcNow;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var claimed = await _context.ChangeRequests
                .Where(cr => cr.Id == request.Id && cr.Status == ChangeRequestStatus.Approved)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(cr => cr.Status, ChangeRequestStatus.Implemented)
                    .SetProperty(cr => cr.ImplementedBy, normalizedImplementer)
                    .SetProperty(cr => cr.ImplementedAt, implementedAt));

            if (claimed == 0)
            {
                await transaction.RollbackAsync();
                await _context.Entry(request).ReloadAsync();
                throw new InvalidOperationException(
                    $"Change request {requestNumber} cannot be implemented from status {request.Status}; " +
                    "concurrent status change detected");
            }

            request.Status = ChangeRequestStatus.Implemented;
            request.ImplementedBy = normalizedImplementer;
            request.ImplementedAt = implementedAt;

            var links = distinctItemIds.Select(itemId => new ChangeRequestItemLink
            {
                Id = Guid.NewGuid(),
                ChangeRequestId = request.Id,
                ConfigurationItemId = itemId,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            _context.ChangeRequestItemLinks.AddRange(links);

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            _logger.LogInformation("Implemented change request {RequestNumber}", requestNumber);
        }

        private static string NormalizeRequiredText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{fieldName} is required", fieldName);
            }

            return value.Trim();
        }

        private static readonly HashSet<string> AllowedConfigurationRoots = new(StringComparer.OrdinalIgnoreCase)
        {
            "Docs", "Core", "WebAPI", "Certification", "Physics", "AI", "Models", "Aerospace", "Scripts", "Tests"
        };

        private static string NormalizeEvidencePath(string? filePath)
        {
            if (!TryNormalizeEvidencePath(filePath, out var normalized, out var error))
            {
                throw new ArgumentException(error, nameof(filePath));
            }

            return normalized;
        }

        private static bool IsStoredEvidencePathSafe(string? filePath)
        {
            return TryNormalizeEvidencePath(filePath, out var normalized, out _)
                && string.Equals(filePath, normalized, StringComparison.Ordinal);
        }

        private static bool TryNormalizeEvidencePath(string? filePath, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "Configuration item file path is required.";
                return false;
            }

            var trimmed = filePath.Trim().Replace('\\', '/');
            // Reject absolute / UNC / scheme URIs (http://, file:, C:\) so SCI
            // evidence cannot point outside the repository (parity with RTM).
            if (trimmed.StartsWith("/", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.Contains("://", StringComparison.Ordinal)
                || trimmed.Contains(':', StringComparison.Ordinal))
            {
                error = "Configuration item file path must be relative to the repository.";
                return false;
            }

            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0
                || segments.Any(segment => segment is "." or ".."))
            {
                error = "Configuration item file path must not contain traversal segments.";
                return false;
            }

            if (segments.Length < 2 || !AllowedConfigurationRoots.Contains(segments[0]))
            {
                error =
                    "Configuration item file path must be under a repository evidence tree (Docs/, Core/, WebAPI/, Certification/, Physics/, AI/, Models/, Aerospace/, Scripts/, Tests/).";
                return false;
            }

            normalized = string.Join("/", segments);
            return true;
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

            // Empty / unreleased / checksum-free SCI is not DO-178C evidence.
            if (baseline.ConfigurationItems == null || baseline.ConfigurationItems.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} has no configuration items; SCI cannot be generated without configuration evidence");
            }

            // Leftover outside-tree items must not appear as official SCI evidence.
            if (baseline.ConfigurationItems.Any(bci => !IsStoredEvidencePathSafe(bci.ConfigurationItem.FilePath)))
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} contains configuration items outside the repository evidence tree; SCI cannot be generated");
            }

            if (!HasReleasedChecksumEvidence(baseline.ConfigurationItems))
            {
                throw new InvalidOperationException(
                    $"Baseline {baseline.BaselineName} cannot produce an SCI until every configuration item is Released with a checksum");
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

                if (!IsStoredEvidencePathSafe(item.FilePath))
                {
                    report.Issues.Add(new ConfigurationAuditIssue
                    {
                        ItemName = item.ItemName,
                        IssueType = AuditIssueType.UnsafeFilePath,
                        Severity = IssueSeverity.Critical,
                        Description = $"Configuration item {item.ItemName} path is outside the repository evidence tree"
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

        private static string NormalizeActorName(string? actorName) =>
            string.IsNullOrWhiteSpace(actorName) ? string.Empty : actorName.Trim();

        private static string NormalizeActorIdentity(string? actorName, string paramName)
        {
            var normalized = NormalizeActorName(actorName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException($"{paramName} is required", paramName);
            }

            if (IsPlaceholderActor(normalized))
            {
                throw new ArgumentException(
                    $"{paramName} must be a real actor identity, not a placeholder such as 'System'",
                    paramName);
            }

            return normalized;
        }

        private static bool HasRealActorIdentity(string? actorName)
        {
            var normalized = NormalizeActorName(actorName);
            return !string.IsNullOrWhiteSpace(normalized) && !IsPlaceholderActor(normalized);
        }

        private static bool IsPlaceholderActor(string actorName)
        {
            var normalized = actorName.Trim().ToLowerInvariant();
            return normalized is "system" or "unknown" or "n/a" or "na" or "none" or "anonymous";
        }

        /// <summary>
        /// Reject vacuous CCB notes ("ok", "lgtm", "approved") that previously recorded
        /// independent approval without a real disposition.
        /// </summary>
        internal static bool HasSubstantiveApprovalNotes(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return false;

            var trimmed = notes.Trim();
            if (trimmed.Length < 12)
                return false;

            var normalized = trimmed.ToLowerInvariant();
            return normalized is not (
                "done" or "fixed" or "ok" or "okay" or "approved" or "lgtm" or
                "n/a" or "na" or "none" or "complete" or "completed" or "pass" or "passed" or
                "ccb ok" or "looks good" or "looks good to me");
        }

        private async Task<string> AllocateNextRequestNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"CR-{year}-";
            var existing = await _context.ChangeRequests
                .Where(cr => cr.RequestNumber.StartsWith(prefix))
                .Select(cr => cr.RequestNumber)
                .ToListAsync();

            // Numeric max — not lexicographic OrderByDescending (CR-…-10000 < CR-…-9999 as strings).
            var next = 1;
            foreach (var number in existing)
            {
                if (number.Length > prefix.Length
                    && int.TryParse(number.AsSpan(prefix.Length), out var parsed)
                    && parsed >= next)
                {
                    next = parsed + 1;
                }
            }

            return $"{prefix}{next:D4}";
        }

        private static bool HasReleasedChecksumEvidence(IEnumerable<BaselineConfigurationItem> links) =>
            links.All(link =>
                link.ConfigurationItem != null &&
                link.ConfigurationItem.Status == ConfigurationItemStatus.Released &&
                !string.IsNullOrWhiteSpace(link.ConfigurationItem.Checksum));
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
        BaselineNotApproved,
        UnsafeFilePath
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
