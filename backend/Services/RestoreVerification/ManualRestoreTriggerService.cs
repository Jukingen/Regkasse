using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.OperationalRuns;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.RestoreVerification;

public sealed class ManualRestoreTriggerService : IManualRestoreTriggerService
{
    private readonly AppDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly IComplianceCheckService _complianceCheck;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ManualRestoreTargetDatabaseGuard _targetGuard;
    private readonly IManualRestoreApprovalNotificationService _notification;
    private readonly IOptionsMonitor<ManualRestoreApprovalOptions> _options;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _restoreOptions;
    private readonly ILogger<ManualRestoreTriggerService> _logger;

    public ManualRestoreTriggerService(
        AppDbContext db,
        IAuditLogService audit,
        IComplianceCheckService complianceCheck,
        ICurrentTenantAccessor tenantAccessor,
        ManualRestoreTargetDatabaseGuard targetGuard,
        IManualRestoreApprovalNotificationService notification,
        IOptionsMonitor<ManualRestoreApprovalOptions> options,
        IOptionsMonitor<RestoreVerificationOptions> restoreOptions,
        ILogger<ManualRestoreTriggerService> logger)
    {
        _db = db;
        _audit = audit;
        _complianceCheck = complianceCheck;
        _tenantAccessor = tenantAccessor;
        _targetGuard = targetGuard;
        _notification = notification;
        _options = options;
        _restoreOptions = restoreOptions;
        _logger = logger;
    }

    public async Task<RestoreRequestStatus> CreateRequestAsync(
        RestoreRequest request,
        string actorUserId,
        string? actorEmail,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        if (!request.ValidationOnly)
            throw new ArgumentException("ValidationOnly must be true; production restore is not supported.", nameof(request));

        var targetDb = request.TargetDatabaseName.Trim().ToLowerInvariant();
        _targetGuard.ValidateOrThrow(targetDb);

        // Pre-restore RKSV compliance: same-tenant, dump integrity, validation gates.
        var operatingTenantId = _tenantAccessor.TenantId ?? Guid.Empty;
        var compliance = await _complianceCheck.CheckRestoreComplianceAsync(
            request.BackupRunId,
            operatingTenantId,
            cancellationToken);
        if (!compliance.Succeeded)
        {
            if (string.Equals(compliance.Code, RestoreService.CrossTenantCode, StringComparison.Ordinal))
            {
                // Cross-tenant → 404 (not 403), consistent with tenant isolation semantics.
                throw new KeyNotFoundException($"Backup run {request.BackupRunId} was not found.");
            }

            if (string.Equals(compliance.Code, ComplianceCheckService.BackupNotFoundCode, StringComparison.Ordinal))
                throw new KeyNotFoundException(compliance.Error ?? $"Backup run {request.BackupRunId} was not found.");

            if (string.Equals(compliance.Code, RestoreService.ProductionRestoreCode, StringComparison.Ordinal))
                throw new ArgumentException(compliance.Error, nameof(request));

            throw new InvalidOperationException(compliance.Error ?? "Restore compliance check failed.");
        }

        var ttlMinutes = Math.Max(1, _options.CurrentValue.ApprovalTokenTtlMinutes);
        var ttl = TimeSpan.FromMinutes(ttlMinutes);
        var plainToken = ManualRestoreApprovalTokenHasher.GenerateSixDigitToken();
        var entity = new ManualRestoreRequest
        {
            Status = ManualRestoreRequestStatus.PendingApproval,
            BackupRunId = request.BackupRunId,
            TargetDatabaseName = targetDb,
            ValidationOnly = true,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            ApprovalTokenHash = ManualRestoreApprovalTokenHasher.Hash(plainToken),
            ApprovalTokenExpiresAtUtc = DateTime.UtcNow.Add(ttl),
            RequestedByUserId = actorUserId,
            RequestedByEmail = actorEmail,
            CorrelationId = correlationId,
        };

        _db.ManualRestoreRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var approverEmails = await ResolveApproverEmailsAsync(actorUserId, cancellationToken);
        var notificationSent = await _notification.SendApprovalTokenAsync(
            approverEmails,
            plainToken,
            new ManualRestoreApprovalNotificationContext(
                entity.Id,
                actorEmail ?? actorUserId,
                request.BackupRunId,
                targetDb,
                entity.ApprovalTokenExpiresAtUtc!.Value,
                entity.Reason),
            cancellationToken);

        await ManualRestoreAudit.LogRequestedAsync(
            _audit,
            actorUserId,
            entity,
            sourceBackupTenantId: compliance.TenantId,
            requiresApproval: true,
            correlationId,
            notes: notificationSent > 0
                ? $"Approval token sent to {notificationSent} recipient(s) via email."
                : "Approval token not sent (SMTP unconfigured or no approvers).");

        _logger.LogInformation(
            "Manual restore request created: requestId={RequestId}, backupRunId={BackupRunId}, targetDb={TargetDb}, approverEmails={EmailCount}",
            entity.Id,
            entity.BackupRunId,
            entity.TargetDatabaseName,
            approverEmails.Count);

        return ToStatusDto(entity);
    }

    public async Task<RestoreRequestStatus> ProcessApprovalAsync(
        Guid requestId,
        RestoreApprovalRequest request,
        string actorUserId,
        string? actorEmail,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        if (string.IsNullOrWhiteSpace(request.ApprovalToken))
            throw new ArgumentException("Approval token is required.", nameof(request));

        if (!ManualRestoreApprovalTokenHasher.IsValidSixDigitFormat(request.ApprovalToken))
            throw new ArgumentException("Approval token must be a 6-digit code.", nameof(request));

        var action = request.Action?.Trim();
        if (string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase))
            return await ApproveInternalAsync(requestId, request.ApprovalToken, actorUserId, correlationId, cancellationToken);

        if (string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ArgumentException("Reason is required when action is reject.", nameof(request));
            return await RejectInternalAsync(
                requestId,
                request.ApprovalToken,
                request.Reason,
                actorUserId,
                correlationId,
                cancellationToken);
        }

        throw new ArgumentException("Action must be 'approve' or 'reject'.", nameof(request));
    }

    private async Task<RestoreRequestStatus> ApproveInternalAsync(
        Guid requestId,
        string approvalToken,
        string approverUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadPendingForApprovalAsync(requestId, approvalToken, approverUserId, cancellationToken);

        entity.Status = ManualRestoreRequestStatus.Approved;
        entity.ApprovedByUserId = approverUserId;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovalTokenHash = null;

        var drillRun = await EnqueueRestoreVerificationRunAsync(entity, approverUserId, correlationId, cancellationToken);
        entity.Status = ManualRestoreRequestStatus.Executing;
        entity.RestoreVerificationRunId = drillRun.Id;

        await _db.SaveChangesAsync(cancellationToken);

        var sourceTenantId = await ResolveSourceBackupTenantIdAsync(entity.BackupRunId, cancellationToken);
        await ManualRestoreAudit.LogApprovedAsync(
            _audit,
            approverUserId,
            entity,
            sourceTenantId,
            drillRun.Id,
            correlationId);

        _logger.LogInformation(
            "Manual restore approved: requestId={RequestId}, drillRunId={DrillRunId}, approver={ApproverId}",
            entity.Id,
            drillRun.Id,
            approverUserId);

        return ToStatusDto(entity);
    }

    private async Task<RestoreRequestStatus> RejectInternalAsync(
        Guid requestId,
        string approvalToken,
        string rejectionReason,
        string actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadPendingForApprovalAsync(requestId, approvalToken, actorUserId, cancellationToken);

        entity.Status = ManualRestoreRequestStatus.Rejected;
        entity.RejectionReason = rejectionReason.Trim();
        entity.ApprovedByUserId = actorUserId;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovalTokenHash = null;
        await _db.SaveChangesAsync(cancellationToken);

        var sourceTenantId = await ResolveSourceBackupTenantIdAsync(entity.BackupRunId, cancellationToken);
        await ManualRestoreAudit.LogRejectedAsync(
            _audit,
            actorUserId,
            entity,
            sourceTenantId,
            entity.RejectionReason ?? rejectionReason,
            correlationId);

        return ToStatusDto(entity);
    }

    private async Task<ManualRestoreRequest> LoadPendingForApprovalAsync(
        Guid requestId,
        string approvalToken,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var entity = await _db.ManualRestoreRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (entity == null)
            throw new KeyNotFoundException($"Manual restore request {requestId} was not found.");

        if (entity.Status != ManualRestoreRequestStatus.PendingApproval)
            throw new InvalidOperationException($"Request is not pending approval (status: {entity.Status}).");

        if (string.Equals(entity.RequestedByUserId, actorUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("A different Super Admin must approve or reject this request.");

        if (entity.ApprovalTokenExpiresAtUtc is null
            || entity.ApprovalTokenExpiresAtUtc < DateTime.UtcNow)
        {
            throw new ManualRestoreApprovalException(
                ManualRestoreApprovalException.ExpiredTokenCode,
                "Approval token expired.");
        }

        if (entity.ApprovalTokenHash == null
            || !ManualRestoreApprovalTokenHasher.Verify(approvalToken, entity.ApprovalTokenHash))
        {
            throw new ManualRestoreApprovalException(
                ManualRestoreApprovalException.InvalidTokenCode,
                "Invalid approval token.");
        }

        return entity;
    }

    public async Task<RestoreRequestStatus?> GetStatusAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ManualRestoreRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (entity == null)
            return null;

        if (entity.Status == ManualRestoreRequestStatus.Executing)
        {
            await SyncExecutionOutcomeAsync(entity, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ToStatusDto(entity);
    }

    public async Task<RestoreRequestHistoryResponse> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ManualRestoreRequests.AsNoTracking().OrderByDescending(r => r.RequestedAt);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new RestoreRequestHistoryResponse
        {
            Items = rows.Select(ToStatusDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private async Task<RestoreVerificationRun> EnqueueRestoreVerificationRunAsync(
        ManualRestoreRequest entity,
        string approverUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var active = await _db.RestoreVerificationRuns.AsNoTracking()
            .Where(r => r.Status == RestoreVerificationStatus.Queued || r.Status == RestoreVerificationStatus.Running)
            .AnyAsync(cancellationToken);
        if (active)
            throw new InvalidOperationException(
                "Another restore verification run is already queued or running; wait for it to finish.");

        var ro = _restoreOptions.CurrentValue;
        var run = new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Queued,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            SourceBackupRunId = entity.BackupRunId,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = approverUserId,
            CorrelationId = correlationId ?? entity.CorrelationId,
            IdempotencyKey = $"manual-restore:{entity.Id:N}",
            DetailsJson = ManualRestoreRunDetailsJson.Build(entity.Id, entity.TargetDatabaseName),
            ConfigSnapshotJson = OperationalRunConfigSnapshotBuilder.SerializeRestore(
                ro,
                "manual_restore_approved_enqueue",
                DateTime.UtcNow)
        };

        _db.RestoreVerificationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return run;
    }

    private async Task SyncExecutionOutcomeAsync(ManualRestoreRequest entity, CancellationToken cancellationToken)
    {
        if (entity.Status != ManualRestoreRequestStatus.Executing
            || entity.RestoreVerificationRunId is not Guid drillId)
            return;

        var drill = await _db.RestoreVerificationRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == drillId, cancellationToken);
        if (drill == null)
            return;

        if (drill.Status == RestoreVerificationStatus.Queued
            || drill.Status == RestoreVerificationStatus.Running)
            return;

        var priorStatus = entity.Status;
        if (drill.Status == RestoreVerificationStatus.Succeeded)
        {
            entity.Status = ManualRestoreRequestStatus.Completed;
            entity.Result = BuildResultSummary(drill, succeeded: true);
        }
        else if (drill.Status == RestoreVerificationStatus.Failed)
        {
            entity.Status = ManualRestoreRequestStatus.Failed;
            entity.Result = BuildResultSummary(drill, succeeded: false);
        }

        if (priorStatus == ManualRestoreRequestStatus.Executing
            && entity.Status is ManualRestoreRequestStatus.Completed or ManualRestoreRequestStatus.Failed)
        {
            var actor = entity.ApprovedByUserId ?? entity.RequestedByUserId ?? "system";
            var sourceTenantId = await ResolveSourceBackupTenantIdAsync(entity.BackupRunId, cancellationToken);
            await ManualRestoreAudit.LogExecutionOutcomeAsync(
                _audit,
                actor,
                entity,
                sourceTenantId,
                entity.Status == ManualRestoreRequestStatus.Completed,
                entity.CorrelationId);
        }
    }

    private async Task<Guid?> ResolveSourceBackupTenantIdAsync(
        Guid backupRunId,
        CancellationToken cancellationToken)
    {
        return await _db.BackupRuns.AsNoTracking()
            .Where(r => r.Id == backupRunId)
            .Select(r => r.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildResultSummary(RestoreVerificationRun drill, bool succeeded)
    {
        var payload = new
        {
            drillRunId = drill.Id,
            succeeded,
            drill.Status,
            drill.FailureCode,
            drill.RestoreAttemptPassed,
            drill.PostRestoreContinuityChecksPassed,
            drill.CompletedAt
        };
        return JsonSerializer.Serialize(payload);
    }

    private async Task<List<string>> ResolveApproverEmailsAsync(
        string requesterUserId,
        CancellationToken cancellationToken)
    {
        var emails = await _db.Users.AsNoTracking()
            .Where(u => u.Role == Roles.SuperAdmin && u.Id != requesterUserId)
            .Where(u => u.Email != null && u.Email != "")
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (emails.Count == 0)
        {
            emails = (_options.CurrentValue.FallbackApproverEmails ?? Array.Empty<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return emails;
    }

    private void EnsureEnabled()
    {
        if (!_options.CurrentValue.Enabled)
            throw new InvalidOperationException("Manual restore approval workflow is disabled.");
    }

    private static RestoreRequestStatus ToStatusDto(ManualRestoreRequest entity) =>
        new()
        {
            RequestId = entity.Id,
            Status = MapStatusName(entity.Status),
            RequestedAt = entity.RequestedAt,
            RequestedByUserId = entity.RequestedByUserId,
            RequestedByEmail = entity.RequestedByEmail,
            ApprovalToken = null,
            ApprovedByUserId = entity.ApprovedByUserId,
            ApprovedAt = entity.ApprovedAt,
            RejectionReason = entity.RejectionReason,
            Reason = entity.Reason,
            Result = entity.Result,
            BackupRunId = entity.BackupRunId,
            TargetDatabaseName = entity.TargetDatabaseName,
            ValidationOnly = entity.ValidationOnly,
            RestoreVerificationRunId = entity.RestoreVerificationRunId,
        };

    private static string MapStatusName(ManualRestoreRequestStatus status) => status switch
    {
        ManualRestoreRequestStatus.PendingApproval => "PendingApproval",
        ManualRestoreRequestStatus.Approved => "Approved",
        ManualRestoreRequestStatus.Rejected => "Rejected",
        ManualRestoreRequestStatus.Executing => "Executing",
        ManualRestoreRequestStatus.Completed => "Completed",
        ManualRestoreRequestStatus.Failed => "Failed",
        _ => status.ToString()
    };
}
