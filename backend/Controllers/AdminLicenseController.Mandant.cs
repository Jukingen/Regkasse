using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Mandantenlizenz overview for the effective tenant (Manager self-service on <c>/admin/license</c>).
    /// </summary>
    [HttpGet("mandant")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(TenantLicenseOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLicenseOverviewDto>> GetMandantOverview(
        CancellationToken cancellationToken = default)
    {
        var (tenantId, error) = await ResolveAccessibleMandantTenantIdAsync(null, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return error;

        var overview = await _adminTenantLicenseService
            .GetOverviewAsync(tenantId!.Value, cancellationToken)
            .ConfigureAwait(false);
        if (overview == null)
            return NotFound(new { message = "Tenant not found." });

        return Ok(overview);
    }

    /// <summary>
    /// Extend the effective tenant license with a REGK key (Manager self-service; Super Admin may also set <c>validUntilUtc</c>).
    /// </summary>
    [HttpPost("mandant/extend")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(ExtendTenantLicenseResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExtendTenantLicenseResultDto>> ExtendMandantLicense(
        [FromBody] ExtendTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, error) = await ResolveAccessibleMandantTenantIdAsync(null, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return error;

        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        if (!isSuperAdmin && request.ValidUntilUtc.HasValue)
        {
            return BadRequest(new { message = "validUntilUtc is determined by the license key and cannot be set manually." });
        }

        if (!isSuperAdmin && string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            return BadRequest(new { message = "licenseKey is required." });
        }

        var actorUserId = User.GetActorUserId();
        var actorRole = User.GetActorRole();
        var (result, extendError) = await _adminTenantLicenseService
            .ExtendAsync(tenantId!.Value, request, actorUserId, actorRole, cancellationToken)
            .ConfigureAwait(false);

        if (extendError == "Tenant not found.")
            return NotFound(new { message = extendError });
        if (extendError != null)
            return BadRequest(new { message = extendError });

        _logger.LogInformation(
            "Mandant license extended for tenant {TenantId} by user {ActorUserId}",
            tenantId,
            actorUserId ?? "(unknown)");

        return Ok(ExtendTenantLicenseResultDto.FromOverview(result!));
    }

    /// <summary>Preview mandant license extension for the effective tenant (no mutation).</summary>
    [HttpPost("mandant/preview")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(LicensePreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicensePreviewResult>> PreviewMandantLicense(
        [FromBody] PreviewTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (tenantId, error) = await ResolveAccessibleMandantTenantIdAsync(null, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return error;

        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var (result, previewError) = await _tenantLicenseService
            .PreviewLicenseAsync(tenantId!.Value, request.LicenseKey, isSuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (previewError == "Tenant not found.")
            return NotFound(new { message = previewError });
        if (previewError != null)
            return BadRequest(new { message = previewError });

        return Ok(result);
    }

    /// <summary>
    /// Renew the effective tenant license (Manager self-service; requires payment confirmation).
    /// </summary>
    [HttpPost("mandant/renew")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(LicenseRenewalResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseRenewalResult>> RenewMandantLicense(
        [FromBody] RenewTenantLicenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var (tenantId, error) = await ResolveAccessibleMandantTenantIdAsync(null, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return error;

        if (!request.PaymentConfirmed)
            return BadRequest(new { message = "paymentConfirmed must be true before renewing a tenant license." });

        var actorUserId = User.GetActorUserId();
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? Roles.Manager;

        var result = await _licenseRenewalService
            .RenewLicenseAsync(
                tenantId!.Value,
                request.AdditionalMonths,
                actorUserId,
                actorRole,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (string.Equals(result.Message, "Tenant not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    /// <summary>Mandant license renewal events and issued-license history for a tenant.</summary>
    [HttpGet("history")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(MandantLicenseHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MandantLicenseHistoryResponse>> GetMandantHistory(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        var (resolvedTenantId, error) = await ResolveAccessibleMandantTenantIdAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return error;

        var overview = await _adminTenantLicenseService
            .GetOverviewAsync(resolvedTenantId!.Value, cancellationToken)
            .ConfigureAwait(false);
        if (overview == null)
            return NotFound(new { message = "Tenant not found." });

        var auditItems = await LoadMandantRenewalAuditHistoryAsync(resolvedTenantId.Value, cancellationToken)
            .ConfigureAwait(false);
        var billingItems = await LoadMandantBillingAuditHistoryAsync(resolvedTenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        var merged = overview.History
            .Concat(auditItems)
            .Concat(billingItems)
            .OrderByDescending(i => i.AtUtc)
            .ToList();

        return Ok(new MandantLicenseHistoryResponse(resolvedTenantId.Value, merged));
    }

    private async Task<ActionResult<object>> RenewMandantLicenseAsync(
        RenewLicenseRequestBody body,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            var (resolvedTenantId, accessError) = await ResolveAccessibleMandantTenantIdAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (accessError != null)
                return accessError;
            tenantId = resolvedTenantId!.Value;
        }

        if (!body.PaymentConfirmed)
        {
            return BadRequest(new { message = "paymentConfirmed must be true before renewing a tenant license." });
        }

        if (!body.AdditionalMonths.HasValue || body.AdditionalMonths.Value <= 0)
        {
            return BadRequest(new { message = "additionalMonths must be greater than zero." });
        }

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorRole = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? Roles.Manager;

        var result = await _licenseRenewalService
            .RenewLicenseAsync(
                tenantId,
                body.AdditionalMonths.Value,
                actorUserId,
                actorRole,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (string.Equals(result.Message, "Tenant not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = result.Message });
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    private async Task<IReadOnlyList<TenantLicenseHistoryItemDto>> LoadMandantRenewalAuditHistoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.AuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a =>
                a.TenantId == tenantId
                && (a.ActionType == AuditEventType.LicenseRenewed
                    || a.ActionType == AuditEventType.LicenseExtended
                    || a.ActionType == AuditEventType.LicenseUpdated
                    || a.Action == AuditLogActions.LICENSE_RENEWED
                    || a.Action == AuditLogActions.LICENSE_EXTENDED
                    || a.Action == AuditLogActions.LICENSE_UPDATED))
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new
            {
                a.Timestamp,
                a.Description,
                a.Action,
                a.ActionType,
                a.UserId,
                a.UserRole,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var actorNames = await ResolveUserDisplayNamesAsync(
                rows.Select(r => r.UserId).Where(id => !string.IsNullOrWhiteSpace(id)),
                cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r =>
            {
                var eventType = r.ActionType switch
                {
                    AuditEventType.LicenseExtended => "extended",
                    AuditEventType.LicenseUpdated => "updated",
                    _ => "renewed",
                };
                actorNames.TryGetValue(r.UserId, out var actorName);
                var actorLabel = !string.IsNullOrWhiteSpace(actorName)
                    ? actorName
                    : string.IsNullOrWhiteSpace(r.UserRole) ? null : r.UserRole;

                return new TenantLicenseHistoryItemDto(
                    null,
                    eventType,
                    DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc),
                    string.IsNullOrWhiteSpace(r.Description) ? "License renewed." : r.Description.Trim(),
                    null,
                    string.IsNullOrWhiteSpace(r.UserId) ? null : r.UserId,
                    actorLabel);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<TenantLicenseHistoryItemDto>> LoadMandantBillingAuditHistoryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.BillingAuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(b =>
                b.TenantId == tenantId
                && (b.Action == BillingAuditEventTypes.LicenseActivated
                    || b.Action == BillingAuditEventTypes.LicenseExtended))
            .OrderByDescending(b => b.TimestampUtc)
            .Take(50)
            .Select(b => new
            {
                b.TimestampUtc,
                b.Action,
                b.Details,
                b.UserId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return Array.Empty<TenantLicenseHistoryItemDto>();

        var actorNames = await ResolveUserDisplayNamesAsync(
                rows.Select(r => r.UserId.ToString("D")),
                cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r =>
            {
                var eventType = string.Equals(r.Action, BillingAuditEventTypes.LicenseActivated, StringComparison.Ordinal)
                    ? "activated"
                    : "extended";
                actorNames.TryGetValue(r.UserId.ToString("D"), out var actorName);

                return new TenantLicenseHistoryItemDto(
                    null,
                    eventType,
                    DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc),
                    string.IsNullOrWhiteSpace(r.Details)
                        ? eventType == "activated"
                            ? "License activated."
                            : "License extended."
                        : r.Details.Trim(),
                    null,
                    r.UserId.ToString("D"),
                    string.IsNullOrWhiteSpace(actorName) ? "System" : actorName);
            })
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveUserDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.ToDictionary(
            u => u.Id,
            u =>
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                return string.IsNullOrWhiteSpace(name) ? u.UserName ?? u.Id : name;
            },
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves tenant id for mandant license APIs. Non–Super Admin users may only access their effective tenant (404 otherwise).
    /// </summary>
    private async Task<(Guid? TenantId, ActionResult? Error)> ResolveAccessibleMandantTenantIdAsync(
        Guid? requestedTenantId,
        CancellationToken cancellationToken)
    {
        Guid effectiveTenantId;
        try
        {
            effectiveTenantId = await _settingsTenantResolver
                .ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve effective tenant for mandant license API");
            return (null, NotFound(new { message = "Tenant not found." }));
        }

        if (effectiveTenantId == Guid.Empty)
            return (null, NotFound(new { message = "Tenant not found." }));

        if (User.IsInRole(Roles.SuperAdmin))
        {
            var tenantId = requestedTenantId ?? effectiveTenantId;
            if (tenantId == Guid.Empty)
                return (null, BadRequest(new { message = "tenantId is required." }));

            var exists = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
                return (null, NotFound(new { message = "Tenant not found." }));

            return (tenantId, null);
        }

        if (requestedTenantId.HasValue && requestedTenantId.Value != effectiveTenantId)
            return (null, NotFound(new { message = "Tenant not found." }));

        return (effectiveTenantId, null);
    }
}

/// <summary>Payload for <c>GET /api/admin/license/history?tenantId=</c>.</summary>
public sealed record MandantLicenseHistoryResponse(
    Guid TenantId,
    IReadOnlyList<TenantLicenseHistoryItemDto> Items);
