using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin read model for the Peppol outbox. No UBL and no API key.
/// Does not submit, poll, or enable <c>EInvoicing.Peppol</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/peppol/submissions")]
[Produces("application/json")]
public sealed class AdminPeppolSubmissionsController : ControllerBase
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
    public const string InvoicePathPrefix = "/invoices?invoiceId=";

    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        EinvoiceSubmissionStatuses.Queued,
        EinvoiceSubmissionStatuses.Sent,
        EinvoiceSubmissionStatuses.Ack,
        EinvoiceSubmissionStatuses.Failed,
    };

    private readonly AppDbContext _db;

    public AdminPeppolSubmissionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PeppolSubmissionListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeppolSubmissionListResponse>> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return NotFound();

        if (!TryNormalizePage(limit, offset, out var take, out var skip))
            return BadRequest();

        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (statusFilter is not null && !Statuses.Contains(statusFilter))
            return BadRequest();

        if (tenantId is Guid requested && !await TenantExistsAsync(requested, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var query = _db.EinvoiceSubmissions.IgnoreQueryFilters().AsNoTracking();
        if (tenantId is Guid filterTenant)
            query = query.Where(row => row.TenantId == filterTenant);
        if (statusFilter is not null)
            query = query.Where(row => row.Status == statusFilter);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(row => row.CreatedAtUtc)
            .ThenBy(row => row.Id)
            .Skip(skip)
            .Take(take)
            .Select(row => new PeppolSubmissionRowDto(
                row.Id,
                row.TenantId,
                row.InvoiceId,
                row.Status,
                row.CorrelationId,
                row.AttemptedAtUtc,
                row.AckedAtUtc,
                row.FailureReason,
                row.ProviderMessageId,
                row.ProviderStatus))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(new PeppolSubmissionListResponse(rows, total, take, skip));
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PeppolSubmissionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeppolSubmissionDetailDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(Roles.SuperAdmin))
            return NotFound();

        var row = await _db.EinvoiceSubmissions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new PeppolSubmissionDetailDto(
                item.Id,
                item.TenantId,
                item.InvoiceId,
                item.Status,
                item.CorrelationId,
                item.AttemptedAtUtc,
                item.AckedAtUtc,
                item.FailureReason,
                item.ProviderMessageId,
                item.ProviderStatus,
                InvoicePathPrefix + item.InvoiceId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return row is null ? NotFound() : Ok(row);
    }

    private async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(tenant => tenant.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

    private static bool TryNormalizePage(int? limit, int? offset, out int take, out int skip)
    {
        take = limit ?? DefaultLimit;
        skip = offset ?? 0;
        return take is >= 1 and <= MaxLimit && skip >= 0;
    }
}

public sealed record PeppolSubmissionListResponse(
    IReadOnlyList<PeppolSubmissionRowDto> Items,
    int Total,
    int Limit,
    int Offset);

public sealed record PeppolSubmissionRowDto(
    Guid Id,
    Guid TenantId,
    Guid InvoiceId,
    string Status,
    Guid CorrelationId,
    DateTime? AttemptedAtUtc,
    DateTime? AckedAtUtc,
    string? FailureReason,
    string? ProviderMessageId,
    string? ProviderStatus);

public sealed record PeppolSubmissionDetailDto(
    Guid Id,
    Guid TenantId,
    Guid InvoiceId,
    string Status,
    Guid CorrelationId,
    DateTime? AttemptedAtUtc,
    DateTime? AckedAtUtc,
    string? FailureReason,
    string? ProviderMessageId,
    string? ProviderStatus,
    string InvoiceHref);
