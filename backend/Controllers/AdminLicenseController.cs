using System.Security.Claims;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin license issuance, status, and audit APIs. **Activation** is unified at <c>POST /api/license/activate</c>
/// (same contract for POS and FA; see <see cref="LicenseController"/>).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/license")]
[Produces("application/json")]
public sealed partial class AdminLicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ILicenseIssuanceService _licenseIssuanceService;
    private readonly ILicenseRenewalService _licenseRenewalService;
    private readonly IAdminTenantLicenseService _adminTenantLicenseService;
    private readonly AppDbContext _db;
    private readonly IAdminTenantService _adminTenantService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILicenseReminderNotificationStore _licenseReminderNotificationStore;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminLicenseController> _logger;

    public AdminLicenseController(
        ILicenseService licenseService,
        ILicenseIssuanceService licenseIssuanceService,
        ILicenseRenewalService licenseRenewalService,
        IAdminTenantLicenseService adminTenantLicenseService,
        AppDbContext db,
        IAdminTenantService adminTenantService,
        ISettingsTenantResolver settingsTenantResolver,
        ILicenseReminderNotificationStore licenseReminderNotificationStore,
        IAuditLogService auditLogService,
        ILogger<AdminLicenseController> logger)
    {
        _licenseService = licenseService;
        _licenseIssuanceService = licenseIssuanceService;
        _licenseRenewalService = licenseRenewalService;
        _adminTenantLicenseService = adminTenantLicenseService;
        _db = db;
        _adminTenantService = adminTenantService;
        _settingsTenantResolver = settingsTenantResolver;
        _licenseReminderNotificationStore = licenseReminderNotificationStore;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>Current license/trial snapshot for this machine.</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<LicenseStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _licenseService.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        var reminders = _licenseReminderNotificationStore.GetReminders();
        return Ok(status with { Reminders = reminders });
    }

    /// <summary>Current deployment/on-premise license snapshot without tenant overlay.</summary>
    [HttpGet("deployment-status")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<LicenseStatusResponse>> GetDeploymentStatus(CancellationToken cancellationToken)
    {
        var status = await _licenseService.GetCurrentDeploymentStatusAsync(cancellationToken).ConfigureAwait(false);
        var reminders = _licenseReminderNotificationStore.GetReminders();
        return Ok(status with { Reminders = reminders });
    }

    /// <summary>Super-admin SaaS tenant license inventory for the `/admin/licenses` platform page.</summary>
    [HttpGet("tenants")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.LicenseView)]
    [ProducesResponseType(typeof(List<TenantLicenseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TenantLicenseDto>>> GetAllTenantLicenses(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? licenseStatus,
        CancellationToken cancellationToken = default)
    {
        var tenants = await _adminTenantService.ListAsync(includeDeleted: false, cancellationToken).ConfigureAwait(false);

        IEnumerable<AdminTenantListItemDto> filtered = tenants;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(t =>
                t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.Slug.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status)
            && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        var result = filtered
            .Select(MapTenantLicenseDto)
            .ToList();

        if (!string.IsNullOrWhiteSpace(licenseStatus)
            && !string.Equals(licenseStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            result = result
                .Where(r => string.Equals(r.LicenseStatus, licenseStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Ok(result);
    }

    /// <summary>
    /// Paged list of issued licenses (<c>issued_licenses</c>). JWT is never returned.
    /// <c>licenseKey</c> is masked as REGK-****-****- plus the real final segment only.
    /// Optional <paramref name="machineFingerprint"/> filters rows that have a matching <c>activated_licenses</c> machine hash substring.
    /// </summary>
    [HttpGet("list")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(IssuedLicensesListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IssuedLicensesListResponse>> ListIssuedLicenses(
        [FromQuery] string? search = null,
        [FromQuery] string? machineFingerprint = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.IssuedLicenses.AsNoTracking().Where(il => !il.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var safe = SanitizeSqlLikeFragment(search);
            if (safe.Length > 0)
                query = query.Where(il => EF.Functions.ILike(il.CustomerName, $"%{safe}%"));
        }

        if (!string.IsNullOrWhiteSpace(machineFingerprint))
        {
            var fp = SanitizeSqlLikeFragment(machineFingerprint);
            if (fp.Length > 0)
            {
                query = query.Where(il => _db.ActivatedLicenses.Any(a =>
                    a.IsActive
                    && a.LicenseKey.ToUpper() == il.LicenseKey.ToUpper()
                    && a.MachineFingerprint != null
                    && EF.Functions.ILike(a.MachineFingerprint, $"%{fp}%")));
            }
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
                il.IsCancelled,
                il.IsDeleted,
                il.FeaturesJson,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var keyUppers = rows
            .Select(r => r.LicenseKey.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        List<(string LicenseKey, string? MachineFingerprint, DateTime ActivatedAtUtc, DateTime LastSeenAtUtc)> actRows = new();
        if (keyUppers.Count > 0)
        {
            actRows = await _db.ActivatedLicenses.AsNoTracking()
                .Where(a => keyUppers.Contains(a.LicenseKey))
                .Select(a => new ValueTuple<string, string?, DateTime, DateTime>(
                    a.LicenseKey,
                    a.MachineFingerprint,
                    a.ActivatedAtUtc,
                    a.LastSeenAtUtc))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var actByKey = actRows
            .GroupBy(t => t.Item1.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = rows.Select(r =>
        {
            var k = r.LicenseKey.Trim().ToUpperInvariant();
            actByKey.TryGetValue(k, out var list);
            list ??= new List<(string, string?, DateTime, DateTime)>();

            DateTime? lastActivationUtc = list.Count == 0
                ? null
                : list.Max(x => x.Item3);
            string? shortFp = null;
            if (list.Count > 0)
            {
                var chosen = list
                    .OrderByDescending(x => x.Item4)
                    .ThenByDescending(x => x.Item3)
                    .First();
                shortFp = FormatShortMachineFingerprint(chosen.Item2);
            }

            return new IssuedLicenseListItemDto
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
                IsCancelled = r.IsCancelled,
                IsDeleted = r.IsDeleted,
                ActivatedDeviceCount = list.Count,
                LastActivationAtUtc = lastActivationUtc,
                RecentMachineFingerprintShort = shortFp,
                Features = LicenseFeatureIds.TryParseStoredFeatures(r.FeaturesJson)?.ToArray(),
            };
        }).ToList();

        return Ok(new IssuedLicensesListResponse
        {
            Total = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = items,
        });
    }

    private static string SanitizeSqlLikeFragment(string value)
    {
        return new string(value.Trim().Where(c => c is not '%' and not '_' and not '\\').Take(256).ToArray());
    }

    private static TenantLicenseDto MapTenantLicenseDto(AdminTenantListItemDto tenant)
    {
        var (daysRemaining, kindRaw) = TenantLicenseStatusMapper.ComputeKindAndDays(
            tenant.LicenseValidUntilUtc,
            tenant.LicenseKey);

        var normalizedKind = kindRaw switch
        {
            "grace_read_only" => "grace_readonly",
            _ => kindRaw,
        };

        return new TenantLicenseDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            LicenseKey = MaskTenantLicenseKey(tenant.LicenseKey),
            ValidUntil = tenant.LicenseValidUntilUtc,
            Status = tenant.Status,
            IsActive = tenant.IsActive,
            OwnerEmail = tenant.OwnerAdminEmail,
            CreatedAt = tenant.CreatedAt,
            LicenseStatus = normalizedKind,
            DaysRemaining = daysRemaining,
            DaysExpired = daysRemaining is < 0 ? Math.Abs(daysRemaining.Value) : 0,
        };
    }

    /// <summary>First 8 + last 8 hex characters for table display (full SHA-256 is 64 chars).</summary>
    private static string? FormatShortMachineFingerprint(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        var t = hex.Trim();
        if (t.Length <= 16)
            return t;
        return $"{t[..8]}…{t[^8..]}";
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
            RequireFingerprint: body.BindToMachineFingerprint ?? body.RequireFingerprint,
            MachineHashHex: body.MachineHashHex,
            FeatureIds: body.Features);

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
    public async Task<ActionResult<object>> Renew(
        [FromBody] RenewLicenseRequestBody? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new GenerateLicenseResponse(false, null, null, null, "Request body is required."));

        if (body.TenantId is Guid mandantTenantId && mandantTenantId != Guid.Empty)
            return await RenewMandantLicenseAsync(body, mandantTenantId, cancellationToken).ConfigureAwait(false);

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

    private static string? MaskTenantLicenseKey(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;

        var trimmed = licenseKey.Trim();
        if (trimmed.StartsWith("TIER:", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return MaskIssuedLicenseKey(trimmed);
    }
}

/// <summary>Request body for <c>POST /api/admin/license/generate</c>.</summary>
public sealed class GenerateLicenseRequestBody
{
    public string? CustomerName { get; set; }

    public string? ExpiryDate { get; set; }

    public bool RequireFingerprint { get; set; }

    public string? MachineHashHex { get; set; }

    /// <summary>Optional explicit feature bundle; omitted = full single-license bundle.</summary>
    public string[]? Features { get; set; }

    [JsonPropertyName("bindToMachineFingerprint")]
    public bool? BindToMachineFingerprint { get; set; }
}

/// <summary>Response payload for <c>POST /api/admin/license/generate</c>.</summary>
public sealed record GenerateLicenseResponse(
    bool Success,
    string? LicenseKey,
    string? SignedJwt,
    DateTime? ExpiryAtUtc,
    string? Message)
{
    /// <summary>Same as <see cref="SignedJwt"/>; stable alias for newer clients.</summary>
    public string? LicenseJwt => SignedJwt;
}

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
    /// <summary>Mandant SaaS renewal: tenant row id (mutually exclusive with issued-license fields).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Months to add for mandant renewal (required when <see cref="TenantId"/> is set).</summary>
    [Range(1, 120)]
    public int? AdditionalMonths { get; set; }

    /// <summary>Mandant renewal requires explicit payment confirmation.</summary>
    public bool PaymentConfirmed { get; set; }

    /// <summary>Full REGK key of the row to renew (mutually exclusive with <see cref="IssuedLicenseId"/>).</summary>
    public string? LicenseKey { get; set; }

    /// <summary>Row id from <c>GET /api/admin/license/list</c> (mutually exclusive with <see cref="LicenseKey"/>).</summary>
    public Guid? IssuedLicenseId { get; set; }

    /// <summary>Issued-license renewal end date (YYYY-MM-DD UTC end-of-day); required for issued path.</summary>
    public string? NewExpiryDate { get; set; }
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

    /// <summary>Rows in <c>activated_licenses</c> for this license key (distinct machines).</summary>
    public int ActivatedDeviceCount { get; set; }

    /// <summary>Latest <c>activated_at_utc</c> across activations for this key.</summary>
    public DateTime? LastActivationAtUtc { get; set; }

    /// <summary>Shortened fingerprint (first 8 + last 8 hex) of the device with the latest <c>last_seen_at_utc</c>.</summary>
    public string? RecentMachineFingerprintShort { get; set; }

    public bool IsCancelled { get; set; }

    public bool IsDeleted { get; set; }

    /// <summary>Enabled license feature ids when stored on the issuance row; null = full bundle / legacy row.</summary>
    public string[]? Features { get; set; }
}

public sealed class TenantLicenseDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? LicenseKey { get; set; }

    public DateTime? ValidUntil { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? OwnerEmail { get; set; }

    public DateTime CreatedAt { get; set; }

    public string LicenseStatus { get; set; } = "no_license";

    public int? DaysRemaining { get; set; }

    public int DaysExpired { get; set; }
}
