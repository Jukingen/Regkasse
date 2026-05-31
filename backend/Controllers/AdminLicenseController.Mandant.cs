using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>Mandant license renewal events and issued-license history for a tenant.</summary>
    [HttpGet("history")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(MandantLicenseHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MandantLicenseHistoryResponse>> GetMandantHistory(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        var overview = await _adminTenantLicenseService
            .GetOverviewAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (overview == null)
            return NotFound(new { message = "Tenant not found." });

        var auditItems = await LoadMandantRenewalAuditHistoryAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        var merged = overview.History
            .Concat(auditItems)
            .OrderByDescending(i => i.AtUtc)
            .ToList();

        return Ok(new MandantLicenseHistoryResponse(tenantId, merged));
    }

    private async Task<ActionResult<object>> RenewMandantLicenseAsync(
        RenewLicenseRequestBody body,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Mandant license renewal requires SuperAdmin." });
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
            ?? Roles.SuperAdmin;

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
                    || a.Action == AuditLogActions.LICENSE_RENEWED))
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new
            {
                a.Timestamp,
                a.Description,
                a.Action,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new TenantLicenseHistoryItemDto(
                null,
                "renewed",
                DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc),
                string.IsNullOrWhiteSpace(r.Description) ? "License renewed." : r.Description.Trim(),
                null))
            .ToList();
    }
}

/// <summary>Payload for <c>GET /api/admin/license/history?tenantId=</c>.</summary>
public sealed record MandantLicenseHistoryResponse(
    Guid TenantId,
    IReadOnlyList<TenantLicenseHistoryItemDto> Items);
