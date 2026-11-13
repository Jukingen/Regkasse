using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin CH MWST canary status and rollback. One tenant. No secrets.</summary>
[Authorize]
[ApiController]
[Route("api/admin/mwst")]
[Produces("application/json")]
public sealed class AdminMwstCanaryController : ControllerBase
{
    private readonly IOptions<MwstOptions> _mwst;
    private readonly IOptions<KassenSicherheitOptions> _kassenSicherheit;
    private readonly IFeatureFlagService _flags;
    private readonly IAuditLogService _audit;

    public AdminMwstCanaryController(
        IOptions<MwstOptions> mwst,
        IOptions<KassenSicherheitOptions> kassenSicherheit,
        IFeatureFlagService flags,
        IAuditLogService audit)
    {
        _mwst = mwst;
        _kassenSicherheit = kassenSicherheit;
        _flags = flags;
        _audit = audit;
    }

    [HttpGet("canary")]
    [HasPermission(AppPermissions.SystemCritical)]
    public ActionResult<MwstCanaryStatusDto> Get()
    {
        var canary = _mwst.Value.CanaryTenantId?.Trim() ?? string.Empty;
        var enabled = Guid.TryParse(canary, out _)
            && _flags.IsEnabled(FeatureFlagNames.FiscalMwstCh, canary);
        return Ok(new MwstCanaryStatusDto(
            canary,
            enabled,
            _mwst.Value.UseTestEndpoint,
            _kassenSicherheit.Value.Provider ?? "not-configured"));
    }

    [HttpPost("canary/rollback")]
    [HasPermission(AppPermissions.SystemCritical)]
    public async Task<ActionResult<MwstCanaryStatusDto>> Rollback(CancellationToken cancellationToken)
    {
        var canary = _mwst.Value.CanaryTenantId?.Trim() ?? string.Empty;
        if (!Guid.TryParse(canary, out var tenantId))
            return BadRequest(new { message = "Mwst:CanaryTenantId is not set." });

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        await _flags.SetEnabledAsync(
            FeatureFlagNames.FiscalMwstCh,
            enabled: false,
            tenantId: canary,
            actorUserId: actor,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _audit.LogSystemOperationAsync(
            action: "CH_MWST_CANARY_ROLLED_BACK",
            entityType: "Tenant",
            userId: actor,
            userRole: "SuperAdmin",
            description: "Fiscal.MwstCh set false for the CH canary tenant",
            actionType: AuditEventType.ChMwstCanaryRolledBack,
            entityId: tenantId,
            tenantId: tenantId).ConfigureAwait(false);

        return Get();
    }
}

public sealed record MwstCanaryStatusDto(
    string CanaryTenantId,
    bool FlagEnabled,
    bool UseTestEndpoint,
    string KassenSicherheitProvider);
