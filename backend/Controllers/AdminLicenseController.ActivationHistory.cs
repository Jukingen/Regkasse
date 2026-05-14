using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>Paged activation audit for <c>POST /api/admin/license/activate</c> (full key in DB; response uses masked key).</summary>
    [HttpGet("activation-attempts")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseActivationAttemptsPagedResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseActivationAttemptsPagedResponse>> ListActivationAttempts(
        [FromQuery] string? licenseKey = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? status = null,
        [FromQuery] string? machineFingerprint = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.LicenseActivationAttempts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            var k = licenseKey.Trim().ToUpperInvariant();
            var safe = SanitizeSqlLikeFragment(k);
            if (safe.Length > 0)
                q = q.Where(a => EF.Functions.ILike(a.LicenseKey, $"%{safe}%"));
        }

        if (fromUtc.HasValue)
            q = q.Where(a => a.ActivatedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            q = q.Where(a => a.ActivatedAtUtc <= toUtc.Value);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<LicenseActivationAttemptStatus>(status.Trim(), ignoreCase: true, out var st))
        {
            q = q.Where(a => a.ActivationStatus == st);
        }

        if (!string.IsNullOrWhiteSpace(machineFingerprint))
        {
            var fp = SanitizeSqlLikeFragment(machineFingerprint);
            if (fp.Length > 0)
                q = q.Where(a => EF.Functions.ILike(a.MachineFingerprint, $"%{fp}%"));
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await q
            .OrderByDescending(a => a.ActivatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(a => new LicenseActivationAttemptListItemDto
            {
                Id = a.Id,
                LicenseKeyMasked = MaskIssuedLicenseKey(a.LicenseKey),
                MachineFingerprint = a.MachineFingerprint,
                ActivationStatus = a.ActivationStatus.ToString(),
                FailureReason = a.FailureReason,
                ClientIp = a.ClientIp,
                UserAgent = a.UserAgent,
                ActivatedAtUtc = a.ActivatedAtUtc,
                DeactivatedAtUtc = a.DeactivatedAtUtc,
            })
            .ToList();

        return Ok(new LicenseActivationAttemptsPagedResponse
        {
            Total = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = items,
        });
    }

    /// <summary>Removes <c>activated_licenses</c> for the attempt's key + machine and marks the audit row revoked.</summary>
    [HttpPost("activation-attempts/{id:guid}/force-deactivate")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> ForceDeactivateActivationAttempt(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.LicenseActivationAttempts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound();

        if (row.ActivationStatus == LicenseActivationAttemptStatus.Failed)
            return BadRequest(new { message = "Failed attempts cannot be force-deactivated." });

        if (row.DeactivatedAtUtc is not null)
            return BadRequest(new { message = "This activation attempt is already closed." });

        var prevStatus = row.ActivationStatus;
        var prevDeactivated = row.DeactivatedAtUtc;

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "unknown";

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _db.ActivatedLicenses
                .Where(a => EF.Functions.ILike(a.LicenseKey, row.LicenseKey) && a.MachineFingerprint == row.MachineFingerprint)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            row.ActivationStatus = LicenseActivationAttemptStatus.Revoked;
            row.DeactivatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await _auditLogService
                    .LogEntityChangeAsync(
                        "LIC_FORCE_DEACTIVATE_ATTEMPT",
                        nameof(LicenseActivationAttempt),
                        id,
                        actor,
                        role,
                        new { status = prevStatus.ToString(), deactivatedAtUtc = prevDeactivated },
                        new { status = row.ActivationStatus.ToString(), deactivatedAtUtc = row.DeactivatedAtUtc },
                        description: "Admin removed activated_licenses binding from activation audit row.",
                        notes: null)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log failed for force deactivate activation attempt {Id}", id);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation("License activation attempt {Id} force-deactivated by {Actor}", id, actor);
        return Ok();
    }
}

public sealed class LicenseActivationAttemptListItemDto
{
    public Guid Id { get; set; }

    public string LicenseKeyMasked { get; set; } = "";

    public string MachineFingerprint { get; set; } = "";

    public string ActivationStatus { get; set; } = "";

    public string? FailureReason { get; set; }

    public string? ClientIp { get; set; }

    public string? UserAgent { get; set; }

    public DateTime ActivatedAtUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }
}

public sealed class LicenseActivationAttemptsPagedResponse
{
    public int Total { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public IReadOnlyList<LicenseActivationAttemptListItemDto> Items { get; set; } =
        Array.Empty<LicenseActivationAttemptListItemDto>();
}
