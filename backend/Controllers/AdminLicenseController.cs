using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin license status, activation and issuance for on-premise deployments.</summary>
[Authorize]
[ApiController]
[Route("api/admin/license")]
[Produces("application/json")]
public sealed class AdminLicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ILicenseIssuanceService _licenseIssuanceService;
    private readonly AppDbContext _db;
    private readonly ILicenseReminderNotificationStore _licenseReminderNotificationStore;
    private readonly ILogger<AdminLicenseController> _logger;

    public AdminLicenseController(
        ILicenseService licenseService,
        ILicenseIssuanceService licenseIssuanceService,
        AppDbContext db,
        ILicenseReminderNotificationStore licenseReminderNotificationStore,
        ILogger<AdminLicenseController> logger)
    {
        _licenseService = licenseService;
        _licenseIssuanceService = licenseIssuanceService;
        _db = db;
        _licenseReminderNotificationStore = licenseReminderNotificationStore;
        _logger = logger;
    }

    /// <summary>Current license/trial snapshot for this machine.</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.SettingsView)]
    public ActionResult<LicenseStatusResponse> GetStatus()
    {
        var status = _licenseService.GetStatus();
        var reminders = _licenseReminderNotificationStore.GetReminders();
        return Ok(status with { Reminders = reminders });
    }

    /// <summary>
    /// Paged list of issued licenses (<c>issued_licenses</c>). JWT is never returned.
    /// <c>licenseKey</c> is masked as REGK-****-****- plus the real final segment only.
    /// </summary>
    [HttpGet("list")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(IssuedLicensesListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IssuedLicensesListResponse>> ListIssuedLicenses(
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.IssuedLicenses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var safe = new string(search.Trim().Where(c => c is not '%' and not '_' and not '\\').Take(256).ToArray());
            if (safe.Length > 0)
                query = query.Where(il => EF.Functions.ILike(il.CustomerName, $"%{safe}%"));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(il => il.IssuedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(il => new
            {
                il.Id,
                il.LicenseKey,
                il.CustomerName,
                il.ExpiryAtUtc,
                il.RequireFingerprint,
                il.MachineHashHex,
                il.IssuedAtUtc,
                il.IssuedByUserId,
                il.IsRevoked,
                il.RevokedAtUtc,
                il.RevocationReason,
                il.SupersededByLicenseId,
                il.TransferredToLicenseId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(static r => new IssuedLicenseListItemDto
        {
            Id = r.Id,
            LicenseKey = MaskIssuedLicenseKey(r.LicenseKey),
            CustomerName = r.CustomerName,
            ExpiryAtUtc = r.ExpiryAtUtc,
            RequireFingerprint = r.RequireFingerprint,
            MachineHashHex = r.MachineHashHex,
            IssuedAtUtc = r.IssuedAtUtc,
            IssuedByUserId = r.IssuedByUserId,
            IsRevoked = r.IsRevoked,
            RevokedAtUtc = r.RevokedAtUtc,
            RevocationReason = r.RevocationReason,
            SupersededByLicenseId = r.SupersededByLicenseId,
            TransferredToLicenseId = r.TransferredToLicenseId,
        }).ToList();

        return Ok(new IssuedLicensesListResponse
        {
            Total = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = items,
        });
    }

    /// <summary>Activate using a formatted license key and optional offline JWT.</summary>
    [HttpPost("activate")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<LicenseActivationResult>> Activate(
        [FromBody] ActivateLicenseRequest body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new LicenseActivationResult(false, "Request body is required."));

        var result = await _licenseService.ActivateAsync(body, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            _logger.LogWarning("License activation failed: {Message}", result.Message);
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Generate (sign) a new offline license. Requires a deployment with <c>License:SigningPrivateKeyPem</c> configured;
    /// otherwise responds 503. The returned <c>signedJwt</c> is the activation proof — treat as a secret in transit.
    /// </summary>
    [HttpPost("generate")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<GenerateLicenseResponse>> Generate(
        [FromBody] GenerateLicenseRequestBody body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "Request body is required."));

        // ExpiryDate is provided as YYYY-MM-DD; treat as end-of-day UTC so the customer keeps the full last day.
        if (!TryParseExpiryEndOfDayUtc(body.ExpiryDate, out var expiryUtc, out var dateError))
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, dateError));

        var serviceRequest = new GenerateLicenseRequest(
            CustomerName: body.CustomerName ?? string.Empty,
            ExpiryDateUtc: expiryUtc,
            RequireFingerprint: body.RequireFingerprint,
            MachineHashHex: body.MachineHashHex);

        var issuedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var result = await _licenseIssuanceService
                .IssueAsync(serviceRequest, issuedByUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning("License generation rejected: {Message}", result.Message);
                return BadRequest(new GenerateLicenseResponse(
                    false, null, null, null, result.Message));
            }

            return Ok(new GenerateLicenseResponse(
                Success: true,
                LicenseKey: result.LicenseKey,
                SignedJwt: result.SignedJwt,
                ExpiryAtUtc: result.ExpiryAtUtc,
                Message: null));
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            _logger.LogWarning("License generation requested but unavailable: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new GenerateLicenseResponse(
                false, null, null, null, ex.Message));
        }
    }

    /// <summary>
    /// Upgrade / supersede: creates a NEW row + JWT (same customer and fingerprint binding), marks the
    /// previous row superseded (<c>superseded_by_license_id</c>) without revocation. Supply exactly one of
    /// <c>licenseKey</c> (full REGK) or <c>issuedLicenseId</c> (row id when <c>GET list</c> returns masked keys).
    /// Rejects when the stored expiry is after the requested new expiry, or when the row is revoked/already superseded.
    /// </summary>
    [HttpPost("upgrade")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<GenerateLicenseResponse>> Upgrade(
        [FromBody] UpgradeLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "Request body is required."));

        if (!TryParseExpiryEndOfDayUtc(body.NewExpiryDate, out var expiryUtc, out var dateError))
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, dateError));

        var hasId = body.IssuedLicenseId.HasValue && body.IssuedLicenseId.Value != Guid.Empty;
        var hasKey = !string.IsNullOrWhiteSpace(body.LicenseKey);
        if (hasId == hasKey)
        {
            return BadRequest(new GenerateLicenseResponse(
                false, null, null, null, "Provide exactly one of licenseKey or issuedLicenseId."));
        }

        var upgradedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cmd = new UpgradeLicenseCommand(
            hasKey ? body.LicenseKey!.Trim() : null,
            hasId ? body.IssuedLicenseId : null,
            expiryUtc,
            body.Reason);

        try
        {
            var result = await _licenseIssuanceService
                .UpgradeAsync(cmd, upgradedByUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning("License upgrade rejected: {Message}", result.Message);
                return BadRequest(new GenerateLicenseResponse(
                    false, null, null, null, result.Message));
            }

            return Ok(new GenerateLicenseResponse(
                Success: true,
                LicenseKey: result.LicenseKey,
                SignedJwt: result.SignedJwt,
                ExpiryAtUtc: result.ExpiryAtUtc,
                Message: null));
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            _logger.LogWarning("License upgrade requested but unavailable: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new GenerateLicenseResponse(
                false, null, null, null, ex.Message));
        }
    }

    /// <summary>
    /// Original expiry (<c>expiry_at_utc</c>) plus a suggested calendar date (<c>YYYY-MM-DD</c>) for renewal
    /// (anchor = max(active expiry calendar day, UTC today) + one calendar year, end-of-day UTC).
    /// </summary>
    /// <summary>
    /// POS/helpdesk: confirms whether <paramref name="licenseKey"/> may be transferred to a new server (active, non-expired row).
    /// Anonymous so cashiers without admin permissions can self-check before contacting support (no JWT returned).
    /// </summary>
    [HttpGet("transfer-request/{licenseKey}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LicenseTransferRequestInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseTransferRequestInfoResponse>> GetTransferRequestInfo(
        string licenseKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return NotFound();

        var info = await _licenseIssuanceService
            .GetTransferRequestInfoAsync(licenseKey.Trim(), cancellationToken)
            .ConfigureAwait(false);

        if (info is null)
            return NotFound(new { message = "No issued license matches this license key." });

        return Ok(new LicenseTransferRequestInfoResponse(
            info.Eligible,
            info.Message,
            info.CustomerNameMasked,
            info.ExpiryAtUtc,
            info.NewServerRequiresMachineFingerprint,
            info.LicenseKeyMasked));
    }

    /// <summary>
    /// Creates a NEW issued row + JWT (same expiry, same customer); old row gets <c>transferred_to_license_id</c> (audit; not revoked).
    /// Caller must supply the new server's SHA-256 machine fingerprint (<c>newMachineHashHex</c>), typically from <c>/api/health/license</c>.
    /// </summary>
    [HttpPost("transfer")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<GenerateLicenseResponse>> Transfer(
        [FromBody] TransferLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "Request body is required."));

        if (string.IsNullOrWhiteSpace(body.LicenseKey))
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "licenseKey is required."));

        if (string.IsNullOrWhiteSpace(body.NewMachineHashHex))
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "newMachineHashHex is required."));

        var transferredByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cmd = new TransferLicenseCommand(
            body.LicenseKey.Trim(),
            body.NewMachineHashHex.Trim(),
            body.Reason);

        try
        {
            var result = await _licenseIssuanceService
                .TransferAsync(cmd, transferredByUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning("License transfer rejected: {Message}", result.Message);
                return BadRequest(new GenerateLicenseResponse(
                    false, null, null, null, result.Message));
            }

            return Ok(new GenerateLicenseResponse(
                Success: true,
                LicenseKey: result.LicenseKey,
                SignedJwt: result.SignedJwt,
                ExpiryAtUtc: result.ExpiryAtUtc,
                Message: null));
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            _logger.LogWarning("License transfer requested but unavailable: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new GenerateLicenseResponse(
                false, null, null, null, ex.Message));
        }
    }

    [HttpGet("renewal-info/{licenseKey}")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseRenewalInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseRenewalInfoResponse>> GetRenewalInfo(
        string licenseKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return NotFound();

        var info = await _licenseIssuanceService
            .GetRenewalInfoAsync(licenseKey.Trim(), cancellationToken)
            .ConfigureAwait(false);

        if (info is null)
            return NotFound(new { message = "No issued license matches this license key." });

        return Ok(new LicenseRenewalInfoResponse(
            info.OriginalExpiryAtUtc,
            info.SuggestedNewExpiryDate));
    }

    /// <summary>
    /// Renew an issued license: creates a new row (new <c>licenseKey</c> + JWT), revokes the previous row for audit.
    /// Body: <c>newExpiryDate</c> (YYYY-MM-DD, end-of-day UTC) must be in the future. Supply exactly one of
    /// <c>licenseKey</c> (full REGK) or <c>issuedLicenseId</c> (row id from GET list).
    /// </summary>
    [HttpPost("renew")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<GenerateLicenseResponse>> Renew(
        [FromBody] RenewLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "Request body is required."));

        var hasId = body.IssuedLicenseId.HasValue && body.IssuedLicenseId.Value != Guid.Empty;
        var hasKey = !string.IsNullOrWhiteSpace(body.LicenseKey);
        if (hasId == hasKey)
        {
            return BadRequest(new GenerateLicenseResponse(
                false, null, null, null, "Provide exactly one of licenseKey or issuedLicenseId."));
        }

        if (!TryParseExpiryEndOfDayUtc(body.NewExpiryDate, out var expiryUtc, out var dateError))
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, dateError));

        var renewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var cmd = new RenewLicenseCommand(
            hasKey ? body.LicenseKey!.Trim() : null,
            hasId ? body.IssuedLicenseId : null,
            expiryUtc);

        try
        {
            var result = await _licenseIssuanceService
                .RenewAsync(cmd, renewedByUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning("License renewal rejected: {Message}", result.Message);
                return BadRequest(new GenerateLicenseResponse(
                    false, null, null, null, result.Message));
            }

            return Ok(new GenerateLicenseResponse(
                Success: true,
                LicenseKey: result.LicenseKey,
                SignedJwt: result.SignedJwt,
                ExpiryAtUtc: result.ExpiryAtUtc,
                Message: null));
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            _logger.LogWarning("License renewal requested but unavailable: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new GenerateLicenseResponse(
                false, null, null, null, ex.Message));
        }
    }

    /// <summary>
    /// Mark an issued license as revoked. Requires <c>settings.manage</c> (SuperAdmin has the full permission set).
    /// </summary>
    [HttpPost("revoke")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> RevokeAsync(
        [FromBody] RevokeLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.LicenseKey))
            return BadRequest(new { message = "licenseKey is required." });

        var normalizedKey = body.LicenseKey.Trim();
        var row = await _db.IssuedLicenses
            .FirstOrDefaultAsync(
                il => EF.Functions.ILike(il.LicenseKey, normalizedKey),
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound(new { message = "No issued license matches this license key." });

        if (row.IsRevoked)
            return BadRequest(new { message = "This license is already revoked." });

        ApplyRevocationToRow(row, body.Reason);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Issued license revoked: keyPrefix={Prefix} revokedBy={UserId}",
            normalizedKey.Length <= 14 ? normalizedKey : normalizedKey[..14] + "…",
            row.RevokedByUserId ?? "(anonymous)");

        return Ok();
    }

    /// <summary>
    /// Revoke an issued license by row id (<c>GET /api/admin/license/list</c>). Same rules as POST revoke by key.
    /// </summary>
    [HttpDelete("revoke/{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> RevokeById(Guid id, CancellationToken cancellationToken)
    {
        var row = await _db.IssuedLicenses
            .FirstOrDefaultAsync(il => il.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return NotFound(new { message = "No issued license matches this id." });

        if (row.IsRevoked)
            return BadRequest(new { message = "This license is already revoked." });

        ApplyRevocationToRow(row, revocationReasonRaw: null);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Issued license revoked by id: issuedLicenseId={Id} revokedBy={UserId}",
            id,
            row.RevokedByUserId ?? "(anonymous)");

        return Ok();
    }

    private void ApplyRevocationToRow(IssuedLicense row, string? revocationReasonRaw)
    {
        row.IsRevoked = true;
        row.RevokedAtUtc = DateTime.UtcNow;
        row.RevokedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? reason = null;
        if (!string.IsNullOrWhiteSpace(revocationReasonRaw))
        {
            var trimmed = revocationReasonRaw.Trim();
            const int maxReasonLen = 512;
            reason = trimmed.Length > maxReasonLen ? trimmed[..maxReasonLen] : trimmed;
        }

        row.RevocationReason = reason;
    }

    private static bool TryParseExpiryEndOfDayUtc(string? raw, out DateTime expiryUtc, out string? error)
    {
        expiryUtc = default;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "expiryDate is required (YYYY-MM-DD).";
            return false;
        }

        var trimmed = raw.Trim();
        // Accept both bare date (YYYY-MM-DD) and full ISO-8601; full ISO is normalized to its date-only UTC end-of-day.
        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dateOnly))
        {
            expiryUtc = new DateTime(dateOnly.Year, dateOnly.Month, dateOnly.Day, 23, 59, 59, DateTimeKind.Utc);
            return true;
        }

        if (DateTime.TryParse(trimmed,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var anyDate))
        {
            // Same day end-of-day in UTC.
            expiryUtc = new DateTime(anyDate.Year, anyDate.Month, anyDate.Day, 23, 59, 59, DateTimeKind.Utc);
            return true;
        }

        error = "expiryDate must be a valid date (YYYY-MM-DD).";
        return false;
    }

    /// <summary>REGK-****-****- plus last segment only; non-standard shapes are fully redacted.</summary>
    private static string MaskIssuedLicenseKey(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return "REGK-****-****-*****";

        var parts = licenseKey.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4
            && string.Equals(parts[0], "REGK", StringComparison.OrdinalIgnoreCase)
            && parts[3].Length > 0)
        {
            return "REGK-****-****-" + parts[3].ToUpperInvariant();
        }

        return "REGK-****-****-*****";
    }
}

/// <summary>Request body for <c>POST /api/admin/license/generate</c>.</summary>
public sealed record GenerateLicenseRequestBody(
    string? CustomerName,
    string? ExpiryDate,
    bool RequireFingerprint = false,
    string? MachineHashHex = null);

/// <summary>Response payload for <c>POST /api/admin/license/generate</c>.</summary>
public sealed record GenerateLicenseResponse(
    bool Success,
    string? LicenseKey,
    string? SignedJwt,
    DateTime? ExpiryAtUtc,
    string? Message);

/// <summary>Body for <c>POST /api/admin/license/upgrade</c>.</summary>
public sealed class UpgradeLicenseRequestBody
{
    /// <summary>Full REGK key (mutually exclusive with <see cref="IssuedLicenseId"/>).</summary>
    public string? LicenseKey { get; set; }

    /// <summary>Row id from <c>GET /api/admin/license/list</c> (mutually exclusive with <see cref="LicenseKey"/>).</summary>
    public Guid? IssuedLicenseId { get; set; }

    [Required]
    public string NewExpiryDate { get; set; } = "";

    public string? Reason { get; set; }
}

/// <summary>Payload for <c>GET /api/admin/license/renewal-info/{licenseKey}</c>.</summary>
public sealed record LicenseRenewalInfoResponse(
    DateTime OriginalExpiryAtUtc,
    string SuggestedNewExpiryDate);

/// <summary>POS helper response for <c>GET /api/admin/license/transfer-request/{licenseKey}</c>.</summary>
public sealed record LicenseTransferRequestInfoResponse(
    bool Eligible,
    string Message,
    string? CustomerNameMasked,
    DateTime? ExpiryAtUtc,
    bool NewServerRequiresMachineFingerprint,
    string LicenseKeyMasked);

/// <summary>Body for <c>POST /api/admin/license/transfer</c>.</summary>
public sealed class TransferLicenseRequestBody
{
    [Required]
    public string LicenseKey { get; set; } = "";

    [Required]
    public string NewMachineHashHex { get; set; } = "";

    public string? Reason { get; set; }
}

/// <summary>Body for <c>POST /api/admin/license/renew</c>.</summary>
public sealed class RenewLicenseRequestBody
{
    /// <summary>Full REGK key of the row to renew (mutually exclusive with <see cref="IssuedLicenseId"/>).</summary>
    public string? LicenseKey { get; set; }

    /// <summary>Row id from <c>GET /api/admin/license/list</c> (mutually exclusive with <see cref="LicenseKey"/>).</summary>
    public Guid? IssuedLicenseId { get; set; }

    [Required]
    public string NewExpiryDate { get; set; } = "";
}

/// <summary>Body for <c>POST /api/admin/license/revoke</c>.</summary>
public sealed class RevokeLicenseRequestBody
{
    [Required]
    public string LicenseKey { get; set; } = "";

    public string? Reason { get; set; }
}

/// <summary>Paged payload for <c>GET /api/admin/license/list</c>.</summary>
public sealed class IssuedLicensesListResponse
{
    public int Total { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public IReadOnlyList<IssuedLicenseListItemDto> Items { get; set; } = Array.Empty<IssuedLicenseListItemDto>();
}

/// <summary>One masked row from <c>issued_licenses</c> (no JWT).</summary>
public sealed class IssuedLicenseListItemDto
{
    public Guid Id { get; set; }

    public string LicenseKey { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public DateTime ExpiryAtUtc { get; set; }

    public bool RequireFingerprint { get; set; }

    public string? MachineHashHex { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public string? IssuedByUserId { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevocationReason { get; set; }

    public Guid? SupersededByLicenseId { get; set; }

    public Guid? TransferredToLicenseId { get; set; }
}
