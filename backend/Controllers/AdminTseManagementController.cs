using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE fleet management: device inventory, manual provision, revoke.
/// Process health reuses <see cref="Services.Tse.ITseHealthMonitor"/> (no duplicate probe worker).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse-management")]
[Produces("application/json")]
public sealed class AdminTseManagementController : ControllerBase
{
    private readonly ITseProvisioningService _tseProvisioning;
    private readonly ITseBackupService _tseBackup;
    private readonly ITseCertificateService _certificates;

    public AdminTseManagementController(
        ITseProvisioningService tseProvisioning,
        ITseBackupService tseBackup,
        ITseCertificateService certificates)
    {
        _tseProvisioning = tseProvisioning;
        _tseBackup = tseBackup;
        _certificates = certificates;
    }

    /// <summary>Fleet overview + device list for Super Admin dashboard.</summary>
    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseFleetOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseFleetOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        var overview = await _tseProvisioning.GetFleetOverviewAsync(cancellationToken).ConfigureAwait(false);
        return Ok(overview);
    }

    /// <summary>List all TSE device rows (cross-tenant via cash-register join).</summary>
    [HttpGet("devices")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseDeviceFleetItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseDeviceFleetItemDto>>> ListDevices(
        CancellationToken cancellationToken)
    {
        var devices = await _tseProvisioning.ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(devices);
    }

    /// <summary>Manually provision TSE for a tenant (default register) or a specific cash register.</summary>
    [HttpPost("provision")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(ProvisionTseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProvisionTseResponseDto>> Provision(
        [FromBody] ProvisionTseRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new ProvisionTseRequestDto();
        if (body.CashRegisterId is null && body.TenantId is null)
        {
            return BadRequest(new
            {
                code = "TARGET_REQUIRED",
                message = "Provide tenantId or cashRegisterId.",
            });
        }

        TseProvisioningResult result;
        if (body.CashRegisterId is { } registerId && registerId != Guid.Empty)
        {
            result = await _tseProvisioning
                .ProvisionTseForCashRegisterAsync(registerId, force: true, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            result = await _tseProvisioning
                .ProvisionTseForTenantAsync(body.TenantId!.Value, force: true, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!result.IsSuccess)
        {
            return BadRequest(new ProvisionTseResponseDto
            {
                Success = false,
                Outcome = result.Outcome.ToString(),
                Error = result.Error ?? "TSE provisioning failed.",
                Detail = result.Detail,
            });
        }

        return Ok(new ProvisionTseResponseDto
        {
            Success = true,
            Outcome = result.Outcome.ToString(),
            Detail = result.Detail,
            DeviceId = result.Device?.Id,
            SerialNumber = result.Device?.SerialNumber,
            CashRegisterId = result.Device?.KassenId,
        });
    }

    /// <summary>Soft-revoke a TSE device (deactivates; fiscal history retained).</summary>
    [HttpPost("devices/{id:guid}/revoke")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(ProvisionTseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProvisionTseResponseDto>> Revoke(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorId = User.GetActorUserId() ?? "system";
        var result = await _tseProvisioning
            .RevokeTseDeviceAsync(id, actorId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            if (string.Equals(result.Error, "TSE device not found", StringComparison.Ordinal))
                return NotFound(new { code = "TSE_DEVICE_NOT_FOUND", message = result.Error });

            return BadRequest(new ProvisionTseResponseDto
            {
                Success = false,
                Outcome = result.Outcome.ToString(),
                Error = result.Error,
            });
        }

        return Ok(new ProvisionTseResponseDto
        {
            Success = true,
            Outcome = result.Outcome.ToString(),
            Detail = result.Detail,
            DeviceId = result.Device?.Id,
            SerialNumber = result.Device?.SerialNumber,
            CashRegisterId = result.Device?.KassenId,
        });
    }

    /// <summary>Inspect signing-certificate lifecycle for a TSE device.</summary>
    [HttpGet("devices/{id:guid}/certificate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCertificateInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseCertificateInfoDto>> GetCertificate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var info = await _certificates.GetCertificateInfoAsync(id, cancellationToken).ConfigureAwait(false);
        if (info is null)
            return NotFound(new { code = "TSE_DEVICE_NOT_FOUND", message = "TSE device not found." });
        return Ok(info);
    }

    /// <summary>Validate certificate status (expired / revoked / missing material).</summary>
    [HttpPost("devices/{id:guid}/certificate/validate")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCertificateValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseCertificateValidationResultDto>> ValidateCertificate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _certificates.ValidateCertificateAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Attempt certificate renew/sync. Syncs from key provider when material exists;
    /// Soft/Demo rotates local dates; Real without material schedules vendor ops renewal.
    /// </summary>
    [HttpPost("devices/{id:guid}/certificate/renew")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCertificateRenewalResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseCertificateRenewalResultDto>> RenewCertificate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actorId = User.GetActorUserId() ?? "system";
        var result = await _certificates
            .RenewCertificateAsync(id, actorId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Schedule an operator certificate renewal date (UTC).</summary>
    [HttpPost("devices/{id:guid}/certificate/schedule-renewal")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseCertificateRenewalResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TseCertificateRenewalResultDto>> ScheduleCertificateRenewal(
        Guid id,
        [FromBody] ScheduleTseCertificateRenewalRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new ScheduleTseCertificateRenewalRequestDto();
        if (body.RenewalDateUtc == default)
            return BadRequest(new { error = "renewalDateUtc is required." });

        var actorId = User.GetActorUserId() ?? "system";
        var result = await _certificates
            .ScheduleCertificateRenewalAsync(id, body.RenewalDateUtc, actorId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && result.Outcome == "InvalidDate")
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Create an encrypted TSE DR snapshot (devices + signature chain + BelegNr sequences).
    /// Does not include vendor private keys.
    /// </summary>
    [HttpPost("backups")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(CreateTseBackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateTseBackupResponseDto>> CreateBackup(
        [FromBody] CreateTseBackupRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new CreateTseBackupRequestDto();
        if (body.TenantId == Guid.Empty)
            return BadRequest(new { code = "TENANT_REQUIRED", message = "tenantId is required." });

        var actorId = User.GetActorUserId() ?? "system";
        var result = await _tseBackup
            .CreateTseBackupAsync(body.TenantId, actorId, body.Notes, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>List TSE DR backups (optional tenant filter).</summary>
    [HttpGet("backups")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<TseBackupListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TseBackupListItemDto>>> ListBackups(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _tseBackup.ListBackupsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Preview restore impact (warnings, chain downgrade detection).</summary>
    [HttpGet("backups/{id:guid}/preview")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseBackupRestorePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseBackupRestorePreviewDto>> PreviewRestore(
        Guid id,
        CancellationToken cancellationToken)
    {
        var preview = await _tseBackup.PreviewRestoreAsync(id, cancellationToken).ConfigureAwait(false);
        if (preview is null)
            return NotFound(new { code = "TSE_BACKUP_NOT_FOUND", message = "Backup not found." });

        return Ok(preview);
    }

    /// <summary>
    /// Restore TSE device inventory + signature chain from a backup.
    /// Requires confirmToken = RESTORE. Chain downgrades need forceChainDowngrade.
    /// </summary>
    [HttpPost("backups/{id:guid}/restore")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(RestoreTseBackupResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestoreTseBackupResponseDto>> RestoreBackup(
        Guid id,
        [FromBody] RestoreTseBackupRequestDto? body,
        CancellationToken cancellationToken)
    {
        body ??= new RestoreTseBackupRequestDto();
        var actorId = User.GetActorUserId() ?? "system";
        var result = await _tseBackup
            .RestoreTseBackupAsync(id, body, actorId, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            if (string.Equals(result.Error, "Backup not found.", StringComparison.Ordinal))
                return NotFound(new { code = "TSE_BACKUP_NOT_FOUND", message = result.Error });

            return BadRequest(result);
        }

        return Ok(result);
    }
}
