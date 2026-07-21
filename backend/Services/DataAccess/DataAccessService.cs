using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataRights;

namespace KasseAPI_Final.Services.DataAccess;

/// <summary>
/// GDPR data access control: auto-approve View/Export; Delete → pending_approval + Super Admin notify.
/// Persists via <see cref="ICustomerDataRightsService"/> / <see cref="TenantDataRightsRequest"/>.
/// </summary>
public sealed class DataAccessService : IDataAccessService
{
    private readonly ICustomerDataRightsService _rights;
    private readonly IDataAccessNotificationService _notificationService;
    private readonly ILogger<DataAccessService> _logger;

    public DataAccessService(
        ICustomerDataRightsService rights,
        IDataAccessNotificationService notificationService,
        ILogger<DataAccessService> logger)
    {
        _rights = rights;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DataAccessResult> ProcessRequestAsync(
        Guid tenantId,
        DataRequestType type,
        Guid userId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var requestedBy = userId == Guid.Empty ? null : userId.ToString("D");

        try
        {
            // Auto-approve for view/export
            if (type is DataRequestType.View or DataRequestType.Export)
            {
                var rights = await _rights
                    .CreateAsync(tenantId, type.ToRightsType(), reason, requestedBy, ct)
                    .ConfigureAwait(false);

                var request = ToAccessRequest(rights, type);
                request.Status = TenantDataRightsRequestStatuses.Approved;
                if (request.ApprovedAt == null)
                    request.ApprovedAt = rights.ApprovedAtUtc ?? DateTime.UtcNow;

                return await ProcessApprovedAsync(request, rights, ct).ConfigureAwait(false);
            }

            // Manual approval for delete
            if (type == DataRequestType.Delete)
            {
                var rights = await _rights
                    .CreateAsync(tenantId, TenantDataRightsRequestTypes.Delete, reason, requestedBy, ct)
                    .ConfigureAwait(false);

                var request = ToAccessRequest(rights, DataRequestType.Delete);
                request.Status = TenantDataRightsRequestStatuses.PendingApproval;

                await _notificationService.NotifySuperAdminAsync(
                    tenantId,
                    request.Id,
                    subject: "Data deletion request",
                    body: $"Tenant {tenantId} submitted a customer data deletion request (request {request.Id}). Manual approval and a 7-day wait apply; RKSV fiscal data is retained.",
                    ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Data access Delete pending approval. TenantId={TenantId}, RequestId={RequestId}",
                    tenantId,
                    request.Id);

                return DataAccessResult.Pending(request, rights);
            }

            return DataAccessResult.Fail("Unknown data request type.", DataAccessErrorCodes.UnknownType);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Data access request failed. TenantId={TenantId}, Type={Type}",
                tenantId,
                type);
            return DataAccessResult.Fail(ex.Message, DataAccessErrorCodes.ProcessingFailed);
        }
    }

    /// <summary>Post-approval processing for auto-approved View/Export (already executed by rights service).</summary>
    private Task<DataAccessResult> ProcessApprovedAsync(
        DataAccessRequest request,
        TenantDataRightsRequestDto rights,
        CancellationToken ct)
    {
        _ = ct;
        _logger.LogInformation(
            "Data access auto-approved and processed. TenantId={TenantId}, Type={Type}, RequestId={RequestId}, Status={Status}",
            request.TenantId,
            request.Type,
            request.Id,
            rights.Status);

        return Task.FromResult(DataAccessResult.Success(request, rights));
    }

    private static DataAccessRequest ToAccessRequest(TenantDataRightsRequestDto rights, DataRequestType type)
    {
        Guid? requestedBy = null;
        if (!string.IsNullOrWhiteSpace(rights.RequestedByUserId)
            && Guid.TryParse(rights.RequestedByUserId, out var parsed))
        {
            requestedBy = parsed;
        }

        return new DataAccessRequest
        {
            Id = rights.Id,
            TenantId = rights.TenantId,
            Type = type,
            Status = rights.Status,
            RequestedAt = rights.RequestedAtUtc,
            RequestedBy = requestedBy,
            ApprovedAt = rights.ApprovedAtUtc,
            Reason = rights.Reason,
        };
    }
}
