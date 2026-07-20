using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataDeletion;
using KasseAPI_Final.Services.DataExport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.DataRights;

/// <summary>
/// GDPR customer data rights (View / Export / Delete) for mandant data management.
/// Delete delegates to <see cref="IDataDeletionService"/> (RKSV retention + 7-day wait).
/// </summary>
public sealed class CustomerDataRightsService : ICustomerDataRightsService
{
    public const int ExportMaxProcessingHours = 24;
    public const int DeleteConfirmationWaitDays = DataDeletionService.ConfirmationWaitDays;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDataExportService _export;
    private readonly IDataDeletionService _deletion;
    private readonly IDataRightsArtifactStore _artifacts;
    private readonly IOptions<DataExportOptions> _exportOptions;
    private readonly IAuditLogService _audit;
    private readonly ILogger<CustomerDataRightsService> _logger;

    public CustomerDataRightsService(
        IDbContextFactory<AppDbContext> dbFactory,
        IDataExportService export,
        IDataDeletionService deletion,
        IDataRightsArtifactStore artifacts,
        IOptions<DataExportOptions> exportOptions,
        IAuditLogService audit,
        ILogger<CustomerDataRightsService> logger)
    {
        _dbFactory = dbFactory;
        _export = export;
        _deletion = deletion;
        _artifacts = artifacts;
        _exportOptions = exportOptions;
        _audit = audit;
        _logger = logger;
    }

    public IReadOnlyList<DataRightsRequestTypeCatalogItemDto> GetRequestTypeCatalog() =>
    [
        new DataRightsRequestTypeCatalogItemDto
        {
            Type = TenantDataRightsRequestTypes.View,
            Description = "View all data",
            Approval = "Auto",
            ProcessingTime = "Instant",
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            MaxProcessingHours = 0,
        },
        new DataRightsRequestTypeCatalogItemDto
        {
            Type = TenantDataRightsRequestTypes.Export,
            Description = "Download all data",
            Approval = "Auto",
            ProcessingTime = "< 24 hours",
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            MaxProcessingHours = ExportMaxProcessingHours,
        },
        new DataRightsRequestTypeCatalogItemDto
        {
            Type = TenantDataRightsRequestTypes.Delete,
            Description = "Delete all non-RKSV data",
            Approval = "Manual + 7 days",
            ProcessingTime = "7 days",
            ApprovalMode = TenantDataRightsApprovalModes.Manual,
            ConfirmationWaitDays = DeleteConfirmationWaitDays,
        },
    ];

    public async Task<TenantDataRightsRequestDto> CreateAsync(
        Guid tenantId,
        string requestType,
        string? reason,
        string? requestedByUserId,
        CancellationToken ct = default)
    {
        if (!TenantDataRightsRequestTypes.IsKnown(requestType))
            throw new InvalidOperationException("Unknown data rights request type. Use view, export, or delete.");

        var type = TenantDataRightsRequestTypes.Normalize(requestType);
        await EnsureTenantExistsAsync(tenantId, ct).ConfigureAwait(false);

        return type switch
        {
            TenantDataRightsRequestTypes.View =>
                await CreateViewAsync(tenantId, reason, requestedByUserId, ct).ConfigureAwait(false),
            TenantDataRightsRequestTypes.Export =>
                await CreateExportAsync(tenantId, reason, requestedByUserId, ct).ConfigureAwait(false),
            TenantDataRightsRequestTypes.Delete =>
                await CreateDeleteAsync(tenantId, reason, requestedByUserId, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown data rights request type."),
        };
    }

    public async Task<TenantDataRightsRequestDto?> GetAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.TenantDataRightsRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (row == null)
            return null;

        TenantDataDeletionRequestDto? linked = null;
        if (row.LinkedDeletionRequestId is Guid delId)
        {
            var del = await db.TenantDataDeletionRequests.AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == delId, ct)
                .ConfigureAwait(false);
            if (del != null)
                linked = DataDeletionService.Map(del);
        }

        return Map(row, linked);
    }

    public async Task<IReadOnlyList<TenantDataRightsRequestDto>> ListAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.TenantDataRightsRequests.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.RequestedAtUtc)
            .Take(50)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var deletionIds = rows
            .Where(r => r.LinkedDeletionRequestId.HasValue)
            .Select(r => r.LinkedDeletionRequestId!.Value)
            .Distinct()
            .ToList();

        var deletions = deletionIds.Count == 0
            ? new Dictionary<Guid, TenantDataDeletionRequestDto>()
            : await db.TenantDataDeletionRequests.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => deletionIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, DataDeletionService.Map, ct)
                .ConfigureAwait(false);

        return rows.Select(r =>
        {
            TenantDataDeletionRequestDto? linked = null;
            if (r.LinkedDeletionRequestId is Guid id && deletions.TryGetValue(id, out var d))
                linked = d;
            return Map(r, linked);
        }).ToList();
    }

    public async Task<DataRightsExportDownload> DownloadExportAsync(
        Guid tenantId,
        Guid requestId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Data rights request not found.");

        if (!string.Equals(row.RequestType, TenantDataRightsRequestTypes.Export, StringComparison.Ordinal))
            throw new InvalidOperationException("Only export requests can be downloaded.");

        if (row.Status is not (TenantDataRightsRequestStatuses.Ready or TenantDataRightsRequestStatuses.Completed)
            || string.IsNullOrWhiteSpace(row.ArtifactRelativePath))
        {
            throw new InvalidOperationException("Export is not ready for download.");
        }

        var bytes = await _artifacts.ReadAsync(row.ArtifactRelativePath, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Export artifact missing.");

        if (row.Status == TenantDataRightsRequestStatuses.Ready)
        {
            row.Status = TenantDataRightsRequestStatuses.Completed;
            row.CompletedAtUtc = DateTime.UtcNow;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return new DataRightsExportDownload
        {
            FileName = row.ArtifactFileName ?? $"tenant_{tenantId:N}_export.zip",
            Data = bytes,
        };
    }

    public async Task<TenantDataRightsRequestDto> ConfirmDeleteAsync(
        Guid tenantId,
        Guid requestId,
        string? confirmedByUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Data rights request not found.");

        if (!string.Equals(row.RequestType, TenantDataRightsRequestTypes.Delete, StringComparison.Ordinal))
            throw new InvalidOperationException("Only delete requests can be confirmed.");

        if (row.LinkedDeletionRequestId is not Guid delId)
            throw new InvalidOperationException("Delete request is missing linked deletion workflow.");

        var deletion = await _deletion
            .ConfirmDeletionAsync(tenantId, delId, confirmedByUserId, ct)
            .ConfigureAwait(false);

        row.Status = TenantDataRightsRequestStatuses.Confirmed;
        row.UpdatedAt = DateTime.UtcNow;
        row.ProcessingDeadlineUtc = deletion.PurgeEligibleAtUtc
            ?? DateTime.UtcNow.AddDays(DeleteConfirmationWaitDays);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Map(row, deletion);
    }

    public async Task<DeletionResult> ExecuteDeleteAsync(
        Guid tenantId,
        Guid requestId,
        string? actorUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        if (row == null)
            return DeletionResult.Fail("Data rights request not found", DataDeletionErrorCodes.NotFound);

        if (!string.Equals(row.RequestType, TenantDataRightsRequestTypes.Delete, StringComparison.Ordinal))
            return DeletionResult.Fail("Only delete requests can be executed", DataDeletionErrorCodes.InvalidStatus);

        if (row.LinkedDeletionRequestId is not Guid delId)
            return DeletionResult.Fail("Delete request is missing linked deletion workflow", DataDeletionErrorCodes.InvalidStatus);

        var result = await _deletion
            .ExecutePurgeAsync(delId, actorUserId, TenantDataDeletionExecutedVia.Manual, ct)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            row.Status = TenantDataRightsRequestStatuses.Completed;
            row.CompletedAtUtc = DateTime.UtcNow;
            row.CompletedByUserId = actorUserId;
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<int> ProcessPendingExportsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var pending = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .Where(r => r.RequestType == TenantDataRightsRequestTypes.Export
                        && (r.Status == TenantDataRightsRequestStatuses.Processing
                            || r.Status == TenantDataRightsRequestStatuses.Approved
                            || r.Status == TenantDataRightsRequestStatuses.Failed)
                        && (r.ProcessingDeadlineUtc == null || r.ProcessingDeadlineUtc >= now))
            .OrderBy(r => r.RequestedAtUtc)
            .Take(20)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var processed = 0;
        foreach (var row in pending)
        {
            try
            {
                await ProcessExportRowAsync(db, row, ct).ConfigureAwait(false);
                processed++;
            }
            catch (Exception ex)
            {
                row.Status = TenantDataRightsRequestStatuses.Failed;
                row.ErrorMessage = Truncate(ex.Message, 1000);
                row.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                _logger.LogWarning(ex, "Export processing failed. RequestId={RequestId}", row.Id);
            }
        }

        return processed;
    }

    private async Task<TenantDataRightsRequestDto> CreateViewAsync(
        Guid tenantId,
        string? reason,
        string? requestedByUserId,
        CancellationToken ct)
    {
        var summary = await _export.GetSummaryAsync(tenantId, ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;

        var row = new TenantDataRightsRequest
        {
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.View,
            Status = TenantDataRightsRequestStatuses.Completed,
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = now,
            ApprovedAtUtc = now,
            ReadyAtUtc = now,
            CompletedAtUtc = now,
            CompletedByUserId = requestedByUserId,
            Reason = NormalizeReason(reason),
            ViewPayloadJson = JsonSerializer.Serialize(summary, JsonOptions),
            ProcessingDeadlineUtc = now,
            CreatedAt = now,
        };

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.TenantDataRightsRequests.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            action: "TENANT_DATA_RIGHTS_VIEW",
            entityType: AuditLogEntityTypes.SYSTEM_CONFIG,
            userId: requestedByUserId ?? "unknown",
            userRole: "Unknown",
            description: "Customer data rights View completed (instant).",
            status: AuditLogStatus.Success,
            tenantId: tenantId).ConfigureAwait(false);

        var dto = Map(row, linkedDeletion: null);
        dto.ViewSummary = summary;
        return dto;
    }

    private async Task<TenantDataRightsRequestDto> CreateExportAsync(
        Guid tenantId,
        string? reason,
        string? requestedByUserId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = new TenantDataRightsRequest
        {
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.Export,
            Status = TenantDataRightsRequestStatuses.Processing,
            ApprovalMode = TenantDataRightsApprovalModes.Auto,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = now,
            ApprovedAtUtc = now,
            ProcessingDeadlineUtc = now.AddHours(ExportMaxProcessingHours),
            Reason = NormalizeReason(reason),
            CreatedAt = now,
        };

        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.TenantDataRightsRequests.Add(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            await ProcessExportRowAsync(db, row, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Keep processing for background retry within 24h SLA.
            row.Status = TenantDataRightsRequestStatuses.Processing;
            row.ErrorMessage = Truncate(ex.Message, 1000);
            row.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogWarning(
                ex,
                "Export not ready immediately; queued for retry. RequestId={RequestId}",
                row.Id);
        }

        await _audit.LogSystemOperationAsync(
            action: "TENANT_DATA_RIGHTS_EXPORT",
            entityType: AuditLogEntityTypes.SYSTEM_CONFIG,
            userId: requestedByUserId ?? "unknown",
            userRole: "Unknown",
            description: $"Customer data rights Export created (status={row.Status}).",
            status: AuditLogStatus.Success,
            tenantId: tenantId).ConfigureAwait(false);

        return Map(row, linkedDeletion: null);
    }

    private async Task<TenantDataRightsRequestDto> CreateDeleteAsync(
        Guid tenantId,
        string? reason,
        string? requestedByUserId,
        CancellationToken ct)
    {
        var deletion = await _deletion
            .RequestDeletionAsync(tenantId, requestedByUserId, reason, ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        // Idempotent: if an open delete rights request already links this deletion, return it.
        var existing = await db.TenantDataRightsRequests
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId
                        && r.RequestType == TenantDataRightsRequestTypes.Delete
                        && r.LinkedDeletionRequestId == deletion.Id
                        && r.Status != TenantDataRightsRequestStatuses.Cancelled
                        && r.Status != TenantDataRightsRequestStatuses.Completed)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            if (existing.Status == TenantDataRightsRequestStatuses.Pending)
            {
                existing.Status = TenantDataRightsRequestStatuses.PendingApproval;
                existing.UpdatedAt = now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return Map(existing, deletion);
        }

        var create = new TenantDataRightsRequest
        {
            TenantId = tenantId,
            RequestType = TenantDataRightsRequestTypes.Delete,
            Status = TenantDataRightsRequestStatuses.PendingApproval,
            ApprovalMode = TenantDataRightsApprovalModes.Manual,
            RequestedByUserId = requestedByUserId,
            RequestedAtUtc = now,
            Reason = NormalizeReason(reason),
            LinkedDeletionRequestId = deletion.Id,
            ProcessingDeadlineUtc = deletion.PurgeEligibleAtUtc,
            CreatedAt = now,
        };

        db.TenantDataRightsRequests.Add(create);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            action: "TENANT_DATA_RIGHTS_DELETE",
            entityType: AuditLogEntityTypes.SYSTEM_CONFIG,
            userId: requestedByUserId ?? "unknown",
            userRole: "Unknown",
            description: $"Customer data rights Delete requested (deletionId={deletion.Id}).",
            status: AuditLogStatus.Success,
            tenantId: tenantId).ConfigureAwait(false);

        return Map(create, deletion);
    }

    private async Task ProcessExportRowAsync(
        AppDbContext db,
        TenantDataRightsRequest row,
        CancellationToken ct)
    {
        // CreateExportAsync: collect → ZIP → secure store → 7-day link → notify requester.
        await _export.CreateExportAsync(row.Id, ct).ConfigureAwait(false);
        await db.Entry(row).ReloadAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureTenantExistsAsync(Guid tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var exists = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        if (!exists)
            throw new InvalidOperationException($"Tenant {tenantId} not found.");
    }

    private static string MapDeletionStatusToRights(string deletionStatus) =>
        deletionStatus switch
        {
            TenantDataDeletionRequestStatuses.Pending => TenantDataRightsRequestStatuses.PendingApproval,
            TenantDataDeletionRequestStatuses.ExportReady => TenantDataRightsRequestStatuses.PendingApproval,
            TenantDataDeletionRequestStatuses.Confirmed => TenantDataRightsRequestStatuses.Confirmed,
            TenantDataDeletionRequestStatuses.Completed => TenantDataRightsRequestStatuses.Completed,
            TenantDataDeletionRequestStatuses.Cancelled => TenantDataRightsRequestStatuses.Cancelled,
            _ => TenantDataRightsRequestStatuses.PendingApproval,
        };

    private TenantDataRightsRequestDto Map(
        TenantDataRightsRequest row,
        TenantDataDeletionRequestDto? linkedDeletion)
    {
        TenantDataManagementSummaryDto? viewSummary = null;
        if (!string.IsNullOrWhiteSpace(row.ViewPayloadJson)
            && string.Equals(row.RequestType, TenantDataRightsRequestTypes.View, StringComparison.Ordinal))
        {
            try
            {
                viewSummary = JsonSerializer.Deserialize<TenantDataManagementSummaryDto>(
                    row.ViewPayloadJson,
                    JsonOptions);
            }
            catch
            {
                viewSummary = null;
            }
        }

        var canDownload = string.Equals(row.RequestType, TenantDataRightsRequestTypes.Export, StringComparison.Ordinal)
            && row.Status is TenantDataRightsRequestStatuses.Ready or TenantDataRightsRequestStatuses.Completed
            && !string.IsNullOrWhiteSpace(row.ArtifactRelativePath)
            && (row.DownloadExpiresAtUtc == null || row.DownloadExpiresAtUtc > DateTime.UtcNow);

        var canConfirm = string.Equals(row.RequestType, TenantDataRightsRequestTypes.Delete, StringComparison.Ordinal)
            && linkedDeletion != null
            && linkedDeletion.Status is TenantDataDeletionRequestStatuses.Pending
                or TenantDataDeletionRequestStatuses.ExportReady;

        var canExecute = string.Equals(row.RequestType, TenantDataRightsRequestTypes.Delete, StringComparison.Ordinal)
            && linkedDeletion != null
            && linkedDeletion.Status == TenantDataDeletionRequestStatuses.Confirmed
            && linkedDeletion.PurgeEligibleAtUtc.HasValue
            && linkedDeletion.PurgeEligibleAtUtc.Value <= DateTime.UtcNow;

        return new TenantDataRightsRequestDto
        {
            Id = row.Id,
            TenantId = row.TenantId,
            RequestType = row.RequestType,
            Status = row.Status,
            ApprovalMode = row.ApprovalMode,
            Reason = row.Reason,
            RequestedByUserId = row.RequestedByUserId,
            RequestedAtUtc = row.RequestedAtUtc,
            ApprovedAtUtc = row.ApprovedAtUtc,
            ProcessingDeadlineUtc = row.ProcessingDeadlineUtc ?? linkedDeletion?.PurgeEligibleAtUtc,
            ReadyAtUtc = row.ReadyAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            ArtifactFileName = row.ArtifactFileName,
            ArtifactByteSize = row.ArtifactByteSize,
            DownloadLink = string.IsNullOrWhiteSpace(row.DownloadToken)
                ? null
                : DataExportService.BuildDownloadLink(_exportOptions.Value, row.DownloadToken),
            DownloadExpiresAtUtc = row.DownloadExpiresAtUtc,
            CanDownload = canDownload,
            CanConfirm = canConfirm,
            CanExecute = canExecute,
            LinkedDeletionRequestId = row.LinkedDeletionRequestId,
            LinkedDeletionRequest = linkedDeletion,
            ViewSummary = viewSummary,
            ErrorMessage = row.ErrorMessage,
            ConfirmationWaitDays = string.Equals(row.RequestType, TenantDataRightsRequestTypes.Delete, StringComparison.Ordinal)
                ? DeleteConfirmationWaitDays
                : null,
        };
    }

    private static string? NormalizeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
