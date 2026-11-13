using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin DE KassenSicherheit canary. One tenant at a time. Does not sign and does not call Fiskaly.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/kassensicherheit")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminKassenSicherheitController : ControllerBase
{
    public const int RecentTransactionLimit = 20;

    private readonly AppDbContext _db;
    private readonly IFeatureFlagService _flags;
    private readonly IOptions<KassenSicherheitOptions> _options;
    private readonly IAuditLogService _audit;
    private readonly ILogger<AdminKassenSicherheitController> _logger;

    public AdminKassenSicherheitController(
        AppDbContext db,
        IFeatureFlagService flags,
        IOptions<KassenSicherheitOptions> options,
        IAuditLogService audit,
        ILogger<AdminKassenSicherheitController> logger)
    {
        _db = db;
        _flags = flags;
        _options = options;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet("status")]
    public async Task<ActionResult<KassenSicherheitStatusDto>> GetStatus(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId is null || tenantId == Guid.Empty)
            return NotFound();

        if (!await TenantExistsAsync(tenantId.Value, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var dto = await BuildStatusAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        LogCanary(tenantId.Value, transactionId: null, StatusCodes.Status200OK, providerErrorCode: null);
        return Ok(dto);
    }

    [HttpPut("config")]
    public async Task<ActionResult<KassenSicherheitStatusDto>> PutConfig(
        [FromBody] KassenSicherheitConfigRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null || body.TenantId == Guid.Empty)
            return NotFound();

        if (!await TenantExistsAsync(body.TenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var tss = TrimId(body.DeTssId);
        var client = TrimId(body.DeClientId);
        if (tss is null && body.DeTssId is not null && body.DeTssId.Trim().Length > 64)
            return BadRequest(new { message = "DeTssId exceeds 64 characters." });
        if (client is null && body.DeClientId is not null && body.DeClientId.Trim().Length > 64)
            return BadRequest(new { message = "DeClientId exceeds 64 characters." });

        var settings = await _db.CompanySettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == body.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
            return NotFound();

        var tssWasEmpty = string.IsNullOrWhiteSpace(settings.DeTssId);
        var clientWasEmpty = string.IsNullOrWhiteSpace(settings.DeClientId);
        settings.DeTssId = tss;
        settings.DeClientId = client;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var firstFill = (tssWasEmpty && !string.IsNullOrWhiteSpace(tss))
            || (clientWasEmpty && !string.IsNullOrWhiteSpace(client));
        if (firstFill)
        {
            await _audit.LogSystemOperationAsync(
                action: "KS_DE_TSS_CREATED",
                entityType: "CompanySettings",
                userId: ActorId(),
                userRole: "SuperAdmin",
                description: "DE KassenSicherheit TSS or client id stored for the first time",
                actionType: AuditEventType.KsDeTssCreated,
                entityId: settings.Id,
                tenantId: body.TenantId).ConfigureAwait(false);
        }

        LogCanary(body.TenantId, transactionId: null, StatusCodes.Status200OK, providerErrorCode: null);
        return Ok(await BuildStatusAsync(body.TenantId, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("recent-transactions")]
    public async Task<ActionResult<IReadOnlyList<KassenSicherheitRecentTransactionDto>>> Recent(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId is null || tenantId == Guid.Empty)
            return NotFound();

        if (!await TenantExistsAsync(tenantId.Value, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var registerIds = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId.Value)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await _db.PaymentDetails
            .AsNoTracking()
            .Where(p => p.CountryCodeAtIssue == "DE" && registerIds.Contains(p.CashRegisterId))
            .OrderByDescending(p => p.CreatedAt)
            .Take(RecentTransactionLimit)
            .Select(p => new KassenSicherheitRecentTransactionDto(
                p.TransactionId,
                p.ReceiptNumber,
                p.IsStorno ? "storno" : p.IsRefund ? "refund" : p.FinanzOnlineStatus,
                p.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        LogCanary(tenantId.Value, transactionId: rows.FirstOrDefault()?.TransactionId, StatusCodes.Status200OK, providerErrorCode: null);
        return Ok(rows);
    }

    [HttpPost("export-dsfinvk")]
    public async Task<ActionResult<KassenSicherheitExportResultDto>> Export(
        [FromBody] KassenSicherheitExportRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null || body.TenantId == Guid.Empty)
            return NotFound();

        if (!await TenantExistsAsync(body.TenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        await _audit.LogSystemOperationAsync(
            action: "KS_DE_EXPORT_CREATED",
            entityType: "Tenant",
            userId: ActorId(),
            userRole: "SuperAdmin",
            description: "DSFinV-K export requested; status PENDING",
            actionType: AuditEventType.KsDeExportCreated,
            entityId: body.TenantId,
            tenantId: body.TenantId).ConfigureAwait(false);

        LogCanary(body.TenantId, transactionId: null, StatusCodes.Status200OK, providerErrorCode: null);
        return Ok(new KassenSicherheitExportResultDto("PENDING"));
    }

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

    private async Task<KassenSicherheitStatusDto> BuildStatusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantKey = tenantId.ToString("D");
        var statuses = await _flags.GetStatusesAsync(tenantKey, cancellationToken).ConfigureAwait(false);
        var flag = statuses.FirstOrDefault(s =>
            string.Equals(s.Name, FeatureFlagNames.FiscalKassenSicherheitDe, StringComparison.OrdinalIgnoreCase));
        var settings = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new { s.DeTssId, s.DeClientId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var options = _options.Value;
        return new KassenSicherheitStatusDto(
            tenantId,
            flag?.Enabled ?? false,
            string.Equals(flag?.Source, FeatureFlagSources.TenantOverride, StringComparison.Ordinal),
            settings?.DeTssId,
            settings?.DeClientId,
            string.IsNullOrWhiteSpace(options.Provider) ? "not-configured" : options.Provider,
            options.Environment ?? string.Empty);
    }

    private static string? TrimId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length > 64 ? null : trimmed;
    }

    private string ActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private void LogCanary(Guid tenantId, string? transactionId, int httpStatus, string? providerErrorCode)
    {
        _logger.LogInformation(
            "KassenSicherheit canary TenantId={TenantId} TransactionId={TransactionId} HttpStatus={HttpStatus} ProviderErrorCode={ProviderErrorCode}",
            tenantId,
            transactionId,
            httpStatus,
            providerErrorCode);
    }
}

public sealed record KassenSicherheitStatusDto(
    Guid TenantId,
    bool FlagEnabled,
    bool HasTenantOverride,
    string? DeTssId,
    string? DeClientId,
    string Provider,
    string Environment);

public sealed record KassenSicherheitConfigRequest(Guid TenantId, string? DeTssId, string? DeClientId);

public sealed record KassenSicherheitRecentTransactionDto(
    string? TransactionId,
    string ReceiptNumber,
    string? Status,
    DateTime CreatedAt);

public sealed record KassenSicherheitExportRequest(Guid TenantId);

public sealed record KassenSicherheitExportResultDto(string Status);
