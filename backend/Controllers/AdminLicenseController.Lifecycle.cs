using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>SuperAdmin-only issued-license lifecycle routes (<see cref="AppPermissions.LicenseLifecycleSuper"/>).</summary>
public sealed partial class AdminLicenseController
{
    /// <summary>Full issued row + JWT + activation audit (sensitive — SuperAdmin only).</summary>
    [HttpGet("{id:guid}/details")]
    [HasPermission(AppPermissions.LicenseLifecycleSuper)]
    [ProducesResponseType(typeof(IssuedLicenseDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IssuedLicenseDetailResponse>> GetIssuedLicenseDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _db.IssuedLicenses
            .AsNoTracking()
            .FirstOrDefaultAsync(il => il.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound(new { message = "No issued license matches this id." });

        var activations = await _db.ActivatedLicenses.AsNoTracking()
            .Where(a => EF.Functions.ILike(a.LicenseKey, row.LicenseKey))
            .OrderByDescending(a => a.LastSeenAtUtc)
            .Select(a => new IssuedLicenseActivationDto(
                a.MachineFingerprint ?? "",
                a.ActivatedAtUtc,
                a.LastSeenAtUtc,
                a.ValidUntilUtc,
                a.CustomerName ?? ""))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await TryAuditLicenseLifecycleAsync(
                "LIC_DETAILS_VIEW",
                id,
                null,
                new { licenseKeyMasked = MaskIssuedLicenseKey(row.LicenseKey) })
            .ConfigureAwait(false);

        return Ok(new IssuedLicenseDetailResponse(
            row.Id,
            row.LicenseKey,
            row.CustomerName,
            row.ExpiryAtUtc,
            row.RequireFingerprint,
            row.MachineHashHex,
            row.SignedJwt,
            row.IssuedAtUtc,
            row.IssuedByUserId,
            row.IsRevoked,
            row.RevokedAtUtc,
            row.RevocationReason,
            row.SupersededByLicenseId,
            row.TransferredToLicenseId,
            row.IsCancelled,
            row.CancelledAtUtc,
            row.IsDeleted,
            row.DeletedAtUtc,
            activations));
    }

    [HttpPost("{id:guid}/extend")]
    [HasPermission(AppPermissions.LicenseLifecycleSuper)]
    [ProducesResponseType(typeof(GenerateLicenseResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerateLicenseResponse>> ExtendIssuedLicense(
        Guid id,
        [FromBody] ExtendLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "Request body is required."));

        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        GenerateLicenseResult result;
        try
        {
            result = await _licenseIssuanceService
                .ExtendInPlaceByIdAsync(id, body.AddDays, body.AddMonths, actor, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new GenerateLicenseResponse(
                false, null, null, null, ex.Message));
        }

        if (!result.Success)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, result.Message));

        await TryAuditLicenseLifecycleAsync(
                "LIC_EXTEND",
                id,
                null,
                new { addDays = body.AddDays, addMonths = body.AddMonths, newExpiryUtc = result.ExpiryAtUtc })
            .ConfigureAwait(false);

        return Ok(new GenerateLicenseResponse(
            true,
            result.LicenseKey,
            result.SignedJwt,
            result.ExpiryAtUtc,
            null));
    }

    /// <summary>Revokes (blocks) the issued license row by id.</summary>
    [HttpPost("{id:guid}/revoke")]
    [HasPermission(AppPermissions.LicenseLifecycleSuper)]
    public async Task<IActionResult> RevokeIssuedLicenseByIdPost(
        Guid id,
        [FromBody] RevokeLicenseByIdRequestBody? body,
        CancellationToken cancellationToken)
    {
        var row = await _db.IssuedLicenses
            .FirstOrDefaultAsync(il => il.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound(new { message = "No issued license matches this id." });

        if (row.IsDeleted)
            return BadRequest(new { message = "This license row is deleted." });

        if (row.IsCancelled)
            return BadRequest(new { message = "This license is cancelled and cannot be revoked again." });

        if (row.IsRevoked)
            return BadRequest(new { message = "This license is already revoked." });

        ApplyRevocationToRow(row, body?.Reason);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditLicenseLifecycleAsync(
                "LIC_REVOKE",
                id,
                new { isRevoked = false },
                new { isRevoked = true, revokedAtUtc = row.RevokedAtUtc })
            .ConfigureAwait(false);

        return Ok();
    }

    /// <summary>Terminal cancellation: revoked + cancelled flags (no reactivation path on this row).</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.LicenseLifecycleSuper)]
    public async Task<IActionResult> CancelIssuedLicense(
        Guid id,
        [FromBody] CancelLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        var row = await _db.IssuedLicenses
            .FirstOrDefaultAsync(il => il.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound(new { message = "No issued license matches this id." });

        if (row.IsDeleted)
            return BadRequest(new { message = "This license row is deleted." });

        if (row.IsCancelled)
            return BadRequest(new { message = "This license is already cancelled." });

        var reason = string.IsNullOrWhiteSpace(body?.Reason) ? "Cancelled" : body!.Reason!.Trim();
        if (reason.Length > 512)
            reason = reason[..512];

        row.IsCancelled = true;
        row.CancelledAtUtc = DateTime.UtcNow;
        row.CancelledByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!row.IsRevoked)
        {
            row.IsRevoked = true;
            row.RevokedAtUtc = DateTime.UtcNow;
            row.RevokedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        row.RevocationReason = reason;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditLicenseLifecycleAsync(
                "LIC_CANCEL",
                id,
                null,
                new { isCancelled = true, isRevoked = row.IsRevoked })
            .ConfigureAwait(false);

        return Ok();
    }

    /// <summary>Soft-delete: hides from list and blocks validation via registry overlay.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.LicenseLifecycleSuper)]
    public async Task<IActionResult> SoftDeleteIssuedLicense(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.IssuedLicenses
            .FirstOrDefaultAsync(il => il.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound(new { message = "No issued license matches this id." });

        if (row.IsDeleted)
            return BadRequest(new { message = "This license is already deleted." });

        row.IsDeleted = true;
        row.DeletedAtUtc = DateTime.UtcNow;
        row.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        row.IsRevoked = true;
        row.RevokedAtUtc ??= DateTime.UtcNow;
        row.RevokedByUserId ??= User.FindFirstValue(ClaimTypes.NameIdentifier);
        row.RevocationReason ??= "Soft-deleted";

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditLicenseLifecycleAsync(
                "LIC_SOFT_DELETE",
                id,
                new { isDeleted = false },
                new { isDeleted = true, isRevoked = true })
            .ConfigureAwait(false);

        return Ok();
    }

    [HttpPost("{id:guid}/unregister-machine")]
    [HasPermission(AppPermissions.LicenseLifecycleSuper)]
    [ProducesResponseType(typeof(GenerateLicenseResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerateLicenseResponse>> UnregisterMachineForIssuedLicense(
        Guid id,
        CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        GenerateLicenseResult result;
        try
        {
            result = await _licenseIssuanceService
                .UnregisterMachineBindingByIdAsync(id, actor, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new GenerateLicenseResponse(
                false, null, null, null, ex.Message));
        }

        if (!result.Success)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, result.Message));

        await TryAuditLicenseLifecycleAsync(
                "LIC_UNREGISTER_MACHINE",
                id,
                null,
                new { floating = true })
            .ConfigureAwait(false);

        return Ok(new GenerateLicenseResponse(
            true,
            result.LicenseKey,
            result.SignedJwt,
            result.ExpiryAtUtc,
            null));
    }

    private async Task TryAuditLicenseLifecycleAsync(
        string action,
        Guid issuedLicenseId,
        object? oldValues,
        object? newValues)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role) ?? "unknown";
        try
        {
            await _auditLogService
                .LogEntityChangeAsync(
                    action,
                    nameof(IssuedLicense),
                    issuedLicenseId,
                    userId,
                    role,
                    oldValues,
                    newValues,
                    description: $"Issued license lifecycle: {action}",
                    notes: null)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for license lifecycle action {Action} id={Id}", action, issuedLicenseId);
        }
    }
}

/// <summary>Body for <c>POST /api/admin/license/{id}/extend</c>.</summary>
public sealed class ExtendLicenseRequestBody
{
    public int? AddDays { get; set; }

    public int? AddMonths { get; set; }
}

public sealed class RevokeLicenseByIdRequestBody
{
    public string? Reason { get; set; }
}

public sealed class CancelLicenseRequestBody
{
    public string? Reason { get; set; }
}

public sealed record IssuedLicenseActivationDto(
    string MachineFingerprint,
    DateTime ActivatedAtUtc,
    DateTime LastSeenAtUtc,
    DateTime ValidUntilUtc,
    string CustomerName);

public sealed record IssuedLicenseDetailResponse(
    Guid Id,
    string LicenseKey,
    string CustomerName,
    DateTime ExpiryAtUtc,
    bool RequireFingerprint,
    string? MachineHashHex,
    string SignedJwt,
    DateTime IssuedAtUtc,
    string? IssuedByUserId,
    bool IsRevoked,
    DateTime? RevokedAtUtc,
    string? RevocationReason,
    Guid? SupersededByLicenseId,
    Guid? TransferredToLicenseId,
    bool IsCancelled,
    DateTime? CancelledAtUtc,
    bool IsDeleted,
    DateTime? DeletedAtUtc,
    IReadOnlyList<IssuedLicenseActivationDto> Activations);
