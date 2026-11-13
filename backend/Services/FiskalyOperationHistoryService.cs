using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class FiskalyOperationHistoryService : IFiskalyOperationHistoryService
{
    private const int MaxPageSize = 100;
    private const int ExportPageSizeCap = 2000;

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IFiskalyReceiptService _receipts;
    private readonly FiskalyOperationHistoryWriteScope _writeScope;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<FiskalyOperationHistoryService> _logger;

    public FiskalyOperationHistoryService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IFiskalyReceiptService receipts,
        FiskalyOperationHistoryWriteScope writeScope,
        IAuditLogService auditLog,
        ILogger<FiskalyOperationHistoryService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _receipts = receipts;
        _writeScope = writeScope;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<PagedResult<FiskalyOperationHistoryListItemDto>> ListAsync(
        FiskalyOperationHistoryQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, actorIsSuperAdmin ? ExportPageSizeCap : MaxPageSize);

        var source = ApplyFilters(BaseQuery(actorIsSuperAdmin), query, actorIsSuperAdmin);
        var total = await source.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await source
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<FiskalyOperationHistoryListItemDto>
        {
            Items = rows.Select(ToListItem).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<FiskalyOperationHistoryDetailDto?> GetByIdAsync(
        Guid id,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var row = await BaseQuery(actorIsSuperAdmin)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDetail(row);
    }

    public async Task<FiskalyOperationHistoryRetryResultDto> RetryAsync(
        Guid id,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        IQueryable<FiskalyOperationHistory> tracked = _db.FiskalyOperationHistories;
        if (actorIsSuperAdmin)
            tracked = tracked.IgnoreQueryFilters();

        var original = await tracked
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (original is null)
            throw new KeyNotFoundException("Fiskaly operation history entry not found.");

        if (!FiskalyOperationHistoryStatuses.CanRetry(original.Status))
        {
            throw new InvalidOperationException("Only failed or pending operations can be retried.");
        }

        if (string.IsNullOrWhiteSpace(original.RequestPayloadJson))
            throw new InvalidOperationException("Original request payload is missing; retry is not possible.");

        FiskalyHistoryRetryPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<FiskalyHistoryRetryPayload>(
                original.RequestPayloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Original request payload is invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Original request payload is invalid.", ex);
        }

        var cashRegisterId = payload.CashRegisterId == Guid.Empty ? original.CashRegisterId : payload.CashRegisterId;
        if (cashRegisterId == Guid.Empty)
            throw new InvalidOperationException("Cash register id is required to retry.");

        original.RetryCount += 1;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var previousTenantId = _tenantAccessor.TenantId;
        var previousSlug = _tenantAccessor.TenantSlug;
        _tenantAccessor.TenantId = original.TenantId;
        _writeScope.RetriedFromId = original.Id;
        _writeScope.CurrentHistoryId = null;

        FiskalyReceiptOperationResult result;
        try
        {
            result = await ExecuteRetryAsync(
                    original.OperationType,
                    cashRegisterId,
                    payload,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _writeScope.RetriedFromId = null;
            _tenantAccessor.TenantId = previousTenantId;
            _tenantAccessor.TenantSlug = previousSlug;
        }

        try
        {
            await _auditLog
                .LogSystemOperationAsync(
                    action: AuditLogActions.FISKALY_OPERATION_RETRIED,
                    entityType: "FiskalyOperationHistory",
                    userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    userRole: actorIsSuperAdmin ? Roles.SuperAdmin : Roles.Manager,
                    description: $"Fiskaly operation retry ({original.OperationType}).",
                    status: result.Success ? AuditLogStatus.Success : AuditLogStatus.Failed,
                    errorDetails: result.Success ? null : result.Error?.Code,
                    actionType: AuditEventType.FiskalyOperationRetried,
                    tenantId: original.TenantId,
                    entityId: original.Id,
                    newValues: new
                    {
                        OriginalId = original.Id,
                        original.OperationType,
                        RetryHistoryId = _writeScope.CurrentHistoryId,
                        result.Success
                    })
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Fiskaly retry audit for {HistoryId}", original.Id);
        }

        FiskalyOperationHistoryDetailDto? history = null;
        if (_writeScope.CurrentHistoryId is { } newId && newId != Guid.Empty)
            history = await GetByIdAsync(newId, actorIsSuperAdmin, cancellationToken).ConfigureAwait(false);

        return new FiskalyOperationHistoryRetryResultDto
        {
            Operation = new FiskalyReceiptEnvelopeDto
            {
                Success = result.Success,
                Data = result.Data,
                Error = result.Error,
                HistoryId = result.HistoryId
            },
            History = history
        };
    }

    private IQueryable<FiskalyOperationHistory> BaseQuery(bool actorIsSuperAdmin)
    {
        IQueryable<FiskalyOperationHistory> query = _db.FiskalyOperationHistories.AsNoTracking();
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();
        return query;
    }

    private IQueryable<FiskalyOperationHistory> ApplyFilters(
        IQueryable<FiskalyOperationHistory> query,
        FiskalyOperationHistoryQuery filter,
        bool actorIsSuperAdmin)
    {
        if (actorIsSuperAdmin && filter.TenantId is { } tenantFilter && tenantFilter != Guid.Empty)
            query = query.Where(x => x.TenantId == tenantFilter);

        if (filter.FromUtc is { } fromUtc)
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.ToUniversalTime());

        if (filter.ToUtc is { } toUtc)
            query = query.Where(x => x.CreatedAtUtc <= toUtc.ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(filter.OperationType)
            && FiskalyOperationTypes.IsKnown(filter.OperationType))
        {
            var op = filter.OperationType.Trim().ToLowerInvariant();
            query = query.Where(x => x.OperationType == op);
        }

        var status = FiskalyOperationHistoryStatuses.NormalizeFilter(filter.Status);
        if (status is not null)
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var needle = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                (x.ReceiptNumber != null && x.ReceiptNumber.ToLower().Contains(needle))
                || (x.UserDisplayName != null && x.UserDisplayName.ToLower().Contains(needle))
                || x.UserId.ToLower().Contains(needle));
        }

        return query;
    }

    private async Task<FiskalyReceiptOperationResult> ExecuteRetryAsync(
        string operationType,
        Guid cashRegisterId,
        FiskalyHistoryRetryPayload payload,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        return operationType.ToLowerInvariant() switch
        {
            FiskalyOperationTypes.Normal => await _receipts
                .CreateNormalReceiptAsync(
                    cashRegisterId,
                    payload.Amount,
                    payload.VatRate,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Cancel => await _receipts
                .CancelReceiptAsync(
                    cashRegisterId,
                    payload.OriginalReceiptId ?? Guid.Empty,
                    payload.Reason ?? string.Empty,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Nullbeleg => await _receipts
                .CreateNullbelegAsync(
                    cashRegisterId,
                    payload.Year,
                    payload.Month,
                    payload.Reason,
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Startbeleg => await _receipts
                .CreateStartbelegAsync(cashRegisterId, payload.Reason, actorUserId, cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Monatsbeleg => await _receipts
                .CreateMonatsbelegAsync(
                    cashRegisterId,
                    payload.Year ?? 0,
                    payload.Month ?? 0,
                    payload.Reason,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Jahresbeleg => await _receipts
                .CreateJahresbelegAsync(
                    cashRegisterId,
                    payload.Year ?? 0,
                    payload.Reason,
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Schlussbeleg => await _receipts
                .CreateSchlussbelegAsync(cashRegisterId, payload.Reason, actorUserId, cancellationToken)
                .ConfigureAwait(false),
            FiskalyOperationTypes.Tagesabschluss => await _receipts
                .CreateTagesabschlussAsync(
                    payload.ClosingId ?? Guid.Empty,
                    actorUserId,
                    actorIsSuperAdmin,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown operation type '{operationType}'.")
        };
    }

    private static FiskalyOperationHistoryListItemDto ToListItem(FiskalyOperationHistory row) =>
        new()
        {
            Id = row.Id,
            CreatedAtUtc = row.CreatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            OperationType = row.OperationType,
            Status = row.Status,
            CashRegisterId = row.CashRegisterId,
            CashRegisterName = row.CashRegisterName,
            ReceiptNumber = row.ReceiptNumber,
            ReceiptId = row.ReceiptId,
            UserId = row.UserId,
            UserDisplayName = row.UserDisplayName,
            TenantId = row.TenantId,
            TenantName = row.TenantName,
            RetriedFromId = row.RetriedFromId,
            RetryCount = row.RetryCount,
            ErrorCode = row.ErrorCode,
            ErrorMessage = row.ErrorMessage,
            ProgressPercent = FiskalyOperationHistoryStatuses.ProgressPercent(row.Status)
        };

    private static FiskalyOperationHistoryDetailDto ToDetail(FiskalyOperationHistory row) =>
        new()
        {
            Id = row.Id,
            CreatedAtUtc = row.CreatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
            OperationType = row.OperationType,
            Status = row.Status,
            CashRegisterId = row.CashRegisterId,
            CashRegisterName = row.CashRegisterName,
            ReceiptNumber = row.ReceiptNumber,
            ReceiptId = row.ReceiptId,
            UserId = row.UserId,
            UserDisplayName = row.UserDisplayName,
            TenantId = row.TenantId,
            TenantName = row.TenantName,
            RetriedFromId = row.RetriedFromId,
            RetryCount = row.RetryCount,
            ErrorCode = row.ErrorCode,
            ErrorMessage = row.ErrorMessage,
            ProgressPercent = FiskalyOperationHistoryStatuses.ProgressPercent(row.Status),
            RequestPayloadJson = row.RequestPayloadJson,
            ResponsePayloadJson = row.ResponsePayloadJson
        };

    private sealed class FiskalyHistoryRetryPayload
    {
        public Guid CashRegisterId { get; set; }

        public decimal? Amount { get; set; }

        public string? VatRate { get; set; }

        public Guid? OriginalReceiptId { get; set; }

        public string? Reason { get; set; }

        public int? Year { get; set; }

        public int? Month { get; set; }

        public Guid? ClosingId { get; set; }
    }
}
