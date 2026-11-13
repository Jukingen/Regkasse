using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

public sealed class FinanzOnlineOutboxRetryFailedRequest
{
    public string? CashRegisterId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class FinanzOnlineOutboxMarkResolvedRequest
{
    public string? Reason { get; set; }
}

public sealed class FinanzOnlineOutboxActionOutcome
{
    public int StatusCode { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public FinanzOnlineOutboxMessage? Row { get; init; }
    public int RetriedCount { get; init; }

    public static FinanzOnlineOutboxActionOutcome Ok(FinanzOnlineOutboxMessage row) =>
        new() { StatusCode = StatusCodes.Status200OK, Row = row };

    public static FinanzOnlineOutboxActionOutcome Bulk(int count) =>
        new() { StatusCode = StatusCodes.Status200OK, RetriedCount = count };

    public static FinanzOnlineOutboxActionOutcome NotFound() =>
        new()
        {
            StatusCode = StatusCodes.Status404NotFound,
            Code = "FINANZONLINE_OUTBOX_NOT_FOUND",
            Message = "Outbox message not found.",
        };

    public static FinanzOnlineOutboxActionOutcome AlreadySent() =>
        new()
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Code = "FINANZONLINE_OUTBOX_ALREADY_SENT",
            Message = "Outbox message was already sent.",
        };

    public static FinanzOnlineOutboxActionOutcome ReasonRequired() =>
        new()
        {
            StatusCode = StatusCodes.Status400BadRequest,
            Code = "FINANZONLINE_OUTBOX_REASON_REQUIRED",
            Message = "Reason is required.",
        };

    public static FinanzOnlineOutboxActionOutcome InProgress() =>
        new()
        {
            StatusCode = StatusCodes.Status409Conflict,
            Code = "FINANZONLINE_OUTBOX_IN_PROGRESS",
            Message = "Outbox message is being processed.",
        };
}

public interface IFinanzOnlineOutboxActionService
{
    Task<FinanzOnlineOutboxActionOutcome> RetryAsync(Guid id, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    Task<FinanzOnlineOutboxActionOutcome> RetryFailedAsync(
        FinanzOnlineOutboxRetryFailedRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<FinanzOnlineOutboxActionOutcome> MarkResolvedAsync(
        Guid id,
        FinanzOnlineOutboxMarkResolvedRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}

public sealed class FinanzOnlineOutboxActionService : IFinanzOnlineOutboxActionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IAuditLogService _audit;
    private readonly IFinanzOnlineOutboxWorkerSignal _signal;

    public FinanzOnlineOutboxActionService(
        AppDbContext db,
        ICurrentTenantAccessor tenant,
        IAuditLogService audit,
        IFinanzOnlineOutboxWorkerSignal signal)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _signal = signal;
    }

    public async Task<FinanzOnlineOutboxActionOutcome> RetryAsync(
        Guid id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var row = await FindVisibleAsync(id, user, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return FinanzOnlineOutboxActionOutcome.NotFound();
        if (IsAlreadySent(row.Status))
            return FinanzOnlineOutboxActionOutcome.AlreadySent();
        if (row.Status == FinanzOnlineOutboxStatuses.Processing)
            return FinanzOnlineOutboxActionOutcome.InProgress();

        var previousStatus = row.Status;
        var previousAttempts = row.AttemptCount;
        ResetToPending(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _signal.Signal(row.Id);
        await WriteAuditAsync(
            user,
            AuditEventType.FinanzOnlineOutboxRetried,
            row.Id,
            row.CorrelationId,
            row.TenantId,
            new { status = previousStatus, attemptCount = previousAttempts },
            new { status = row.Status, attemptCount = row.AttemptCount }).ConfigureAwait(false);
        return FinanzOnlineOutboxActionOutcome.Ok(row);
    }

    public async Task<FinanzOnlineOutboxActionOutcome> RetryFailedAsync(
        FinanzOnlineOutboxRetryFailedRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!CanSeeRows(user))
            return FinanzOnlineOutboxActionOutcome.NotFound();

        var query = _db.FinanzOnlineOutboxMessages.Where(x =>
            x.Status == FinanzOnlineOutboxStatuses.RetryableFailure
            || x.Status == FinanzOnlineOutboxStatuses.PermanentFailure
            || x.Status == FinanzOnlineOutboxStatuses.ProtocolFailure
            || x.Status == FinanzOnlineOutboxStatuses.DeadLetter
            || x.Status == FinanzOnlineOutboxStatuses.ManualReviewRequired);
        query = ApplyTenant(query, user);
        if (request?.FromUtc is DateTime from)
            query = query.Where(x => x.CreatedAt >= from);
        if (request?.ToUtc is DateTime to)
            query = query.Where(x => x.CreatedAt <= to);

        var rows = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        var register = request?.CashRegisterId?.Trim();
        if (!string.IsNullOrEmpty(register))
            rows = rows.Where(x => string.Equals(TryGetRegisterId(x.PayloadJson), register, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var row in rows)
            ResetToPending(row);

        if (rows.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _signal.Signal();
        }

        await WriteAuditAsync(
            user,
            AuditEventType.FinanzOnlineOutboxBulkRetried,
            entityId: null,
            correlationId: null,
            tenantId: _tenant.TenantId?.ToString(),
            oldValues: null,
            newValues:             new { retriedCount = rows.Count }).ConfigureAwait(false);
        return FinanzOnlineOutboxActionOutcome.Bulk(rows.Count);
    }

    public async Task<FinanzOnlineOutboxActionOutcome> MarkResolvedAsync(
        Guid id,
        FinanzOnlineOutboxMarkResolvedRequest? request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var reason = request?.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            return FinanzOnlineOutboxActionOutcome.ReasonRequired();

        var row = await FindVisibleAsync(id, user, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return FinanzOnlineOutboxActionOutcome.NotFound();

        var previousStatus = row.Status;
        row.Status = FinanzOnlineOutboxStatuses.Resolved;
        row.ResolutionReason = reason.Length > 500 ? reason[..500] : reason;
        var actorId = ActorId(user);
        row.ResolvedByUserId = actorId.Length > 64 ? actorId[..64] : actorId;
        row.ResolvedAtUtc = DateTime.UtcNow;
        row.ProcessingToken = null;
        row.ProcessingStartedAt = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(
            user,
            AuditEventType.FinanzOnlineOutboxMarkedResolved,
            row.Id,
            row.CorrelationId,
            row.TenantId,
            new { status = previousStatus },
            new { status = row.Status, resolvedAtUtc = row.ResolvedAtUtc }).ConfigureAwait(false);
        return FinanzOnlineOutboxActionOutcome.Ok(row);
    }

    internal static bool IsAlreadySent(string status) =>
        status is FinanzOnlineOutboxStatuses.ProtocolSuccess or FinanzOnlineOutboxStatuses.AwaitingProtocol;

    private async Task<FinanzOnlineOutboxMessage?> FindVisibleAsync(
        Guid id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!CanSeeRows(user))
            return null;

        var row = await _db.FinanzOnlineOutboxMessages
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row == null || !RowVisible(row, user))
            return null;
        return row;
    }

    private bool CanSeeRows(ClaimsPrincipal user) =>
        user.IsInRole(Roles.SuperAdmin) || _tenant.TenantId is Guid;

    private IQueryable<FinanzOnlineOutboxMessage> ApplyTenant(
        IQueryable<FinanzOnlineOutboxMessage> query,
        ClaimsPrincipal user)
    {
        // Super Admin acts on any mandant even when FA has an ambient tenant (typically dev).
        if (user.IsInRole(Roles.SuperAdmin))
            return query;
        if (_tenant.TenantId is not Guid ambient)
            return query.Where(_ => false);

        var dashed = ambient.ToString("D");
        var compact = ambient.ToString("N");
        return query.Where(x => x.TenantId == dashed || x.TenantId == compact);
    }

    private bool RowVisible(FinanzOnlineOutboxMessage row, ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.SuperAdmin))
            return true;
        if (_tenant.TenantId is not Guid ambient)
            return false;
        return TenantMatches(row.TenantId, ambient);
    }

    internal static bool TenantMatches(string? rowTenantId, Guid ambient)
    {
        if (string.IsNullOrWhiteSpace(rowTenantId))
            return false;
        if (Guid.TryParse(rowTenantId, out var parsed))
            return parsed == ambient;
        return string.Equals(rowTenantId, ambient.ToString("N"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(rowTenantId, ambient.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }

    private static void ResetToPending(FinanzOnlineOutboxMessage row)
    {
        row.Status = FinanzOnlineOutboxStatuses.Pending;
        row.AttemptCount = 0;
        row.NextAttemptAt = DateTime.UtcNow;
        row.ProcessingToken = null;
        row.ProcessingStartedAt = null;
        row.ProcessedAt = null;
        row.LastErrorCode = null;
        row.LastErrorMessage = null;
        row.FailureCategory = null;
        row.ResolutionReason = null;
        row.ResolvedByUserId = null;
        row.ResolvedAtUtc = null;
    }

    private async Task WriteAuditAsync(
        ClaimsPrincipal user,
        AuditEventType actionType,
        Guid? entityId,
        string? correlationId,
        string? tenantId,
        object? oldValues,
        object? newValues)
    {
        Guid? tenant = null;
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
            tenant = parsed;
        else if (_tenant.TenantId is Guid ambient)
            tenant = ambient;

        await _audit.LogSystemOperationAsync(
            action: actionType.ToString(),
            entityType: nameof(FinanzOnlineOutboxMessage),
            userId: ActorId(user),
            userRole: ActorRole(user),
            correlationIdOverride: correlationId,
            actionType: actionType,
            entityId: entityId,
            tenantId: tenant,
            oldValues: oldValues,
            newValues: newValues).ConfigureAwait(false);
    }

    private static string ActorId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string ActorRole(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) ?? "unknown";

    private static string? TryGetRegisterId(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("Scope", out var scope) &&
                !doc.RootElement.TryGetProperty("scope", out scope))
                return null;
            if (scope.TryGetProperty("RegisterId", out var rid) || scope.TryGetProperty("registerId", out rid))
                return rid.GetString();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
