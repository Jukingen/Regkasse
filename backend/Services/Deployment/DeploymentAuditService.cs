using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Deployment;

public interface IDeploymentAuditService
{
    Task LogFromCiReportAsync(DeploymentCiReportRequest request, CancellationToken cancellationToken = default);

    Task LogRollbackAsync(
        string stage,
        string? version,
        string? previousVersion,
        IReadOnlyList<string>? tenantIds,
        string actor,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task LogComplianceApprovedAsync(
        DeploymentComplianceSignoff signoff,
        CancellationToken cancellationToken = default);
}

public sealed class DeploymentAuditService : IDeploymentAuditService
{
    private readonly IAuditLogService _audit;
    private readonly ILogger<DeploymentAuditService> _logger;

    public DeploymentAuditService(IAuditLogService audit, ILogger<DeploymentAuditService> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    public async Task LogFromCiReportAsync(
        DeploymentCiReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        var (eventType, auditStatus) = status switch
        {
            "deploying" or "pending" or "smoke_running" =>
                (AuditEventType.DeploymentStarted, AuditLogStatus.InProgress),
            "succeeded" or "canary_soak" or "promoted" =>
                (AuditEventType.DeploymentSucceeded, AuditLogStatus.Success),
            "failed" => (AuditEventType.DeploymentFailed, AuditLogStatus.Failed),
            "rolled_back" => (AuditEventType.DeploymentRollback, AuditLogStatus.Success),
            _ => (AuditEventType.DeploymentStarted, AuditLogStatus.Pending),
        };

        var actor = string.IsNullOrWhiteSpace(request.TriggeredBy) ? "ci" : request.TriggeredBy.Trim();
        var payload = new DeploymentAuditPayload
        {
            Stage = request.Stage,
            Status = status,
            Version = request.ImageTag,
            GitSha = request.GitSha,
            RunUrl = request.RunUrl,
            TenantIds = request.TenantIds,
            TriggeredBy = actor,
            ErrorMessage = request.ErrorMessage,
            SmokePassed = request.SmokePassed,
        };

        try
        {
            await _audit.LogSystemOperationAsync(
                GetActionString(eventType),
                AuditLogEntityTypes.DEPLOYMENT,
                actor,
                RolesOrCi(actor),
                description: $"Deployment {status} stage={request.Stage} version={request.ImageTag}",
                notes: request.ErrorMessage,
                status: auditStatus,
                errorDetails: auditStatus == AuditLogStatus.Failed ? request.ErrorMessage : null,
                requestData: payload,
                correlationIdOverride: request.RunUrl,
                actionType: eventType,
                entityName: request.ImageTag).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write deployment audit for status {Status}", status);
        }
    }

    public async Task LogRollbackAsync(
        string stage,
        string? version,
        string? previousVersion,
        IReadOnlyList<string>? tenantIds,
        string actor,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            Stage = stage,
            Version = version,
            PreviousVersion = previousVersion,
            TenantIds = tenantIds,
            TriggeredBy = actor,
            ErrorMessage = errorMessage,
        };

        try
        {
            await _audit.LogSystemOperationAsync(
                AuditLogActions.DEPLOYMENT_ROLLBACK,
                AuditLogEntityTypes.DEPLOYMENT,
                actor,
                RolesOrCi(actor),
                description: $"Deployment rollback stage={stage} to={previousVersion}",
                notes: errorMessage,
                status: AuditLogStatus.Success,
                requestData: payload,
                actionType: AuditEventType.DeploymentRollback,
                entityName: previousVersion ?? version).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write deployment rollback audit");
        }
    }

    public async Task LogComplianceApprovedAsync(
        DeploymentComplianceSignoff signoff,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _audit.LogSystemOperationAsync(
                AuditLogActions.DEPLOYMENT_COMPLIANCE_APPROVED,
                AuditLogEntityTypes.DEPLOYMENT_COMPLIANCE_SIGNOFF,
                signoff.SignedByUserId,
                signoff.SignedByRole ?? Roles.ComplianceOfficer,
                description: $"Compliance sign-off for image={signoff.ImageTag} stage={signoff.Stage}",
                notes: signoff.Notes,
                status: AuditLogStatus.Success,
                requestData: new
                {
                    signoff.ImageTag,
                    signoff.GitSha,
                    signoff.Stage,
                    signoff.ChecklistJson,
                    signoff.SignedAtUtc,
                    signoff.ExpiresAtUtc,
                },
                actionType: AuditEventType.DeploymentComplianceApproved,
                entityId: signoff.Id,
                entityName: signoff.ImageTag).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write compliance approval audit");
        }
    }

    private static string RolesOrCi(string actor) =>
        string.Equals(actor, "ci", StringComparison.OrdinalIgnoreCase)
        || actor.Contains("github", StringComparison.OrdinalIgnoreCase)
            ? "CI"
            : actor;

    private static string GetActionString(AuditEventType type) => type switch
    {
        AuditEventType.DeploymentStarted => AuditLogActions.DEPLOYMENT_STARTED,
        AuditEventType.DeploymentSucceeded => AuditLogActions.DEPLOYMENT_SUCCEEDED,
        AuditEventType.DeploymentFailed => AuditLogActions.DEPLOYMENT_FAILED,
        AuditEventType.DeploymentRollback => AuditLogActions.DEPLOYMENT_ROLLBACK,
        AuditEventType.DeploymentComplianceApproved => AuditLogActions.DEPLOYMENT_COMPLIANCE_APPROVED,
        _ => AuditLogActions.DEPLOYMENT_STARTED,
    };
}
