using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mandanten-Admin RKSV Monatsbeleg sales-blocking policy (not the Super Admin runtime overlay).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/rksv/monatsbeleg-policy")]
[Produces("application/json")]
public sealed class AdminRksvMonatsbelegPolicyController : ControllerBase
{
    private readonly Data.AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IAuditLogService _audit;
    private readonly ILogger<AdminRksvMonatsbelegPolicyController> _logger;

    public AdminRksvMonatsbelegPolicyController(
        Data.AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService audit,
        ILogger<AdminRksvMonatsbelegPolicyController> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(typeof(MonatsbelegPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsbelegPolicyDto>> Get(CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var settings = await _db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        return Ok(Map(settings));
    }

    [HttpPut]
    [HasPermission(AppPermissions.RksvMonatsbelegCreate)]
    [ProducesResponseType(typeof(MonatsbelegPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsbelegPolicyDto>> Put(
        [FromBody] UpdateMonatsbelegPolicyRequest? body,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        body ??= new UpdateMonatsbelegPolicyRequest();
        string? nextMode = null;
        if (body.BlockingMode is not null)
        {
            if (!MonatsbelegBlockingModeNames.TryNormalize(body.BlockingMode, out var normalizedMode))
                return BadRequest(new { message = "Invalid MonatsbelegBlockingMode." });
            nextMode = normalizedMode;
        }

        var settings = await _db.CompanySettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
            return NotFound();

        var oldMode = settings.MonatsbelegBlockingMode;
        var oldAuto = settings.AutoMonatsbelegEnabled;
        var oldRetry = settings.MonatsbelegRetryCount;

        if (nextMode is not null)
            settings.MonatsbelegBlockingMode = nextMode;

        if (body.AutoMonatsbelegEnabled.HasValue)
            settings.AutoMonatsbelegEnabled = body.AutoMonatsbelegEnabled.Value;

        if (body.MonatsbelegRetryCount.HasValue)
            settings.MonatsbelegRetryCount = AutoMonatsbelegCutoff.ClampRetryCount(body.MonatsbelegRetryCount.Value);

        settings.TenantId = tenantId;
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var actor = User.GetActorUserId() ?? "unknown";
        await _audit.LogSystemOperationAsync(
            "MonatsbelegPolicyChanged",
            "company_settings",
            actor,
            Roles.FallbackUnknown,
            description: "Monatsbeleg blocking policy updated",
            actionType: AuditEventType.MonatsbelegPolicyChanged,
            entityId: settings.Id,
            tenantId: tenantId,
            requestData: new
            {
                oldMode,
                newMode = settings.MonatsbelegBlockingMode,
                oldAuto,
                newAuto = settings.AutoMonatsbelegEnabled,
                oldRetry,
                newRetry = settings.MonatsbelegRetryCount,
            });

        _logger.LogInformation(
            "Monatsbeleg policy updated for tenant {TenantId}: mode={Mode} auto={Auto}",
            tenantId,
            settings.MonatsbelegBlockingMode,
            settings.AutoMonatsbelegEnabled);

        return Ok(Map(settings));
    }

    private static MonatsbelegPolicyDto Map(CompanySettings? settings) =>
        new()
        {
            BlockingMode = MonatsbelegBlockingModeNames.ToPersisted(
                MonatsbelegBlockingModeNames.Parse(settings?.MonatsbelegBlockingMode)),
            AutoMonatsbelegEnabled = settings?.AutoMonatsbelegEnabled ?? true,
            MonatsbelegRetryCount = AutoMonatsbelegCutoff.ClampRetryCount(
                settings?.MonatsbelegRetryCount ?? AutoMonatsbelegCutoff.DefaultRetryCount),
            UseDecemberMonatsbelegAsJahresbeleg = settings?.UseDecemberMonatsbelegAsJahresbeleg ?? true,
        };
}
