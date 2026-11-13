using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminCashRegisters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.GracePeriods;

public interface IGracePeriodService
{
    GracePeriodsConfigDto GetConfig();

    Task<IReadOnlyList<GracePeriodPendingDto>> ListActiveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<GracePeriodPendingDto?> GetAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ScheduleGracePeriodResponse> ScheduleAsync(
        Guid tenantId,
        string userId,
        ScheduleGracePeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<ScheduleGracePeriodResponse> CancelAsync(
        Guid tenantId,
        Guid id,
        string userId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Execute due Pending rows (ExpiresAt &lt;= UtcNow). Called by hosted service.</summary>
    Task<int> ExecuteDueAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether an operation-log undo is still inside the configured grace window.</summary>
    bool IsWithinUndoWindow(string actionKind, DateTime createdAtUtc);
}

public sealed class GracePeriodService : IGracePeriodService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<GracePeriodsOptions> _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GracePeriodService> _logger;

    public GracePeriodService(
        AppDbContext db,
        IOptionsMonitor<GracePeriodsOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<GracePeriodService> logger)
    {
        _db = db;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public GracePeriodsConfigDto GetConfig()
    {
        var opts = _options.CurrentValue;
        return new GracePeriodsConfigDto
        {
            Enabled = opts.Enabled,
            Rules =
            [
                MapRule(GracePeriodActionKinds.Schlussbeleg, opts.Schlussbeleg),
                MapRule(GracePeriodActionKinds.TenantDeletion, opts.TenantDeletion),
                MapRule(GracePeriodActionKinds.BulkDelete, opts.BulkDelete),
                MapRule(GracePeriodActionKinds.PriceUpdate, opts.PriceUpdate),
                MapRule(GracePeriodActionKinds.LicenseChange, opts.LicenseChange),
            ],
        };
    }

    public bool IsWithinUndoWindow(string actionKind, DateTime createdAtUtc)
    {
        var rule = _options.CurrentValue.Resolve(actionKind);
        if (rule is null || rule.Duration <= TimeSpan.Zero)
            return false;
        var created = createdAtUtc.Kind == DateTimeKind.Utc
            ? createdAtUtc
            : DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        return DateTime.UtcNow <= created.Add(rule.Duration);
    }

    public async Task<IReadOnlyList<GracePeriodPendingDto>> ListActiveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.GracePeriodPendings.AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.Status == GracePeriodStatuses.Pending)
            .OrderBy(g => g.ExpiresAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(r => MapDto(r, now)).ToList();
    }

    public async Task<GracePeriodPendingDto?> GetAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.GracePeriodPendings.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : MapDto(row, DateTime.UtcNow);
    }

    public async Task<ScheduleGracePeriodResponse> ScheduleAsync(
        Guid tenantId,
        string userId,
        ScheduleGracePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Fail("TENANT_REQUIRED", "Tenant context is required.");
        if (string.IsNullOrWhiteSpace(userId))
            return Fail("AUTH_REQUIRED", "Authentication required.");

        var kind = request.ActionKind?.Trim() ?? string.Empty;
        var rule = _options.CurrentValue.Resolve(kind);
        if (rule is null)
            return Fail("UNKNOWN_ACTION", $"Unknown grace period action '{kind}'.");

        if (rule.Duration <= TimeSpan.Zero)
            return Fail("INVALID_DURATION", "Grace period duration must be positive.");

        var entityId = request.EntityId?.Trim() ?? string.Empty;
        var entityType = request.EntityType?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(entityId) || string.IsNullOrEmpty(entityType))
            return Fail("INVALID_ENTITY", "Entity type and id are required.");

        var duplicate = await _db.GracePeriodPendings.AsNoTracking()
            .AnyAsync(
                g => g.TenantId == tenantId
                     && g.ActionKind == kind
                     && g.EntityId == entityId
                     && g.Status == GracePeriodStatuses.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            return Fail("ALREADY_PENDING", "A grace period is already pending for this entity.");

        var now = DateTime.UtcNow;
        var payload = request.Payload;
        if (string.IsNullOrWhiteSpace(payload) && !string.IsNullOrWhiteSpace(request.Reason))
        {
            payload = JsonSerializer.Serialize(new { reason = request.Reason.Trim() }, JsonOptions);
        }

        var row = new GracePeriodPending
        {
            TenantId = tenantId,
            ActionKind = kind,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload,
            CreatedBy = userId,
            CreatedAt = now,
            ExpiresAt = now.Add(rule.Duration),
            Status = GracePeriodStatuses.Pending,
        };

        _db.GracePeriodPendings.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Grace period scheduled {Id} kind {Kind} entity {EntityType}/{EntityId} expires {ExpiresAt}",
            row.Id,
            kind,
            entityType,
            entityId,
            row.ExpiresAt);

        return new ScheduleGracePeriodResponse
        {
            Success = true,
            Pending = MapDto(row, now),
        };
    }

    public async Task<ScheduleGracePeriodResponse> CancelAsync(
        Guid tenantId,
        Guid id,
        string userId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.GracePeriodPendings
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return Fail("NOT_FOUND", "Grace period not found.");

        if (!string.Equals(row.Status, GracePeriodStatuses.Pending, StringComparison.Ordinal))
            return Fail("NOT_PENDING", "Only pending grace periods can be cancelled.");

        if (DateTime.UtcNow > row.ExpiresAt)
            return Fail("WINDOW_EXPIRED", "The undo window has expired.");

        row.Status = GracePeriodStatuses.Cancelled;
        row.CancelledBy = userId;
        row.CancelledAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
            row.ErrorMessage = reason.Trim();

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Grace period {Id} cancelled by {UserId}", id, userId);

        return new ScheduleGracePeriodResponse
        {
            Success = true,
            Pending = MapDto(row, DateTime.UtcNow),
        };
    }

    public async Task<int> ExecuteDueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var due = await _db.GracePeriodPendings
            .Where(g => g.Status == GracePeriodStatuses.Pending && g.ExpiresAt <= now)
            .OrderBy(g => g.ExpiresAt)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var executed = 0;
        foreach (var row in due)
        {
            try
            {
                await ExecuteOneAsync(row, cancellationToken).ConfigureAwait(false);
                executed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Grace period execute failed {Id} kind {Kind}", row.Id, row.ActionKind);
                row.Status = GracePeriodStatuses.Failed;
                row.ErrorMessage = Truncate(ex.Message, 1000);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return executed;
    }

    private async Task ExecuteOneAsync(GracePeriodPending row, CancellationToken cancellationToken)
    {
        switch (row.ActionKind)
        {
            case GracePeriodActionKinds.Schlussbeleg:
                await ExecuteSchlussbelegAsync(row, cancellationToken).ConfigureAwait(false);
                break;
            case GracePeriodActionKinds.BulkDelete:
            case GracePeriodActionKinds.PriceUpdate:
            case GracePeriodActionKinds.TenantDeletion:
            case GracePeriodActionKinds.LicenseChange:
                // Post-action windows / future schedulers — mark executed without side effects for now.
                row.Status = GracePeriodStatuses.Executed;
                row.ExecutedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;
            default:
                row.Status = GracePeriodStatuses.Failed;
                row.ErrorMessage = $"Unsupported action kind {row.ActionKind}";
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task ExecuteSchlussbelegAsync(GracePeriodPending row, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(row.EntityId, out var registerId))
            throw new InvalidOperationException("Invalid cash register id on grace period.");

        string? reason = null;
        if (!string.IsNullOrWhiteSpace(row.Payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(row.Payload);
                if (doc.RootElement.TryGetProperty("reason", out var r))
                    reason = r.GetString();
            }
            catch (JsonException)
            {
                // ignore
            }
        }

        // Fresh scope so decommission uses its own DbContext / tenant filters.
        using var scope = _scopeFactory.CreateScope();
        var decommission = scope.ServiceProvider.GetRequiredService<ICashRegisterDecommissionService>();
        await decommission
            .DecommissionAsync(registerId, reason, row.CreatedBy, "Admin", cancellationToken)
            .ConfigureAwait(false);

        row.Status = GracePeriodStatuses.Executed;
        row.ExecutedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Grace period {Id} executed Schlussbeleg for register {RegisterId}",
            row.Id,
            registerId);
    }

    private static GracePeriodRuleDto MapRule(string kind, GracePeriodRuleOptions rule) =>
        new()
        {
            ActionKind = kind,
            Duration = rule.Duration.ToString(),
            DurationSeconds = rule.Duration.TotalSeconds,
            RequiresApproval = rule.RequiresApproval,
        };

    private static GracePeriodPendingDto MapDto(GracePeriodPending row, DateTime now)
    {
        var remaining = Math.Max(0, (row.ExpiresAt - now).TotalSeconds);
        var canCancel = string.Equals(row.Status, GracePeriodStatuses.Pending, StringComparison.Ordinal)
                        && remaining > 0;
        return new GracePeriodPendingDto
        {
            Id = row.Id,
            TenantId = row.TenantId,
            ActionKind = row.ActionKind,
            EntityType = row.EntityType,
            EntityId = row.EntityId,
            Status = row.Status,
            CreatedAt = row.CreatedAt,
            ExpiresAt = row.ExpiresAt,
            CanCancel = canCancel,
            RemainingSeconds = remaining,
            CreatedBy = row.CreatedBy,
            OperationLogId = row.OperationLogId,
        };
    }

    private static ScheduleGracePeriodResponse Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, Message = message };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
