using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class CashRegisterOpenRequestService : ICashRegisterOpenRequestService
{
    public const string TenantContextRequiredCode = "TENANT_CONTEXT_REQUIRED";
    public const string RegisterNotFoundCode = "REGISTER_NOT_FOUND";
    public const string RegisterUnavailableCode = "REGISTER_UNAVAILABLE";
    public const string RegisterAssignedToOtherCode = "REGISTER_ASSIGNED_TO_OTHER";
    public const string RegisterAlreadyOpenCode = "REGISTER_ALREADY_OPEN";
    public const string RegisterHeldByOtherCode = "REGISTER_HELD_BY_OTHER";
    public const string AlreadyPendingCode = "OPEN_REQUEST_ALREADY_PENDING";
    public const string RequestNotFoundCode = "OPEN_REQUEST_NOT_FOUND";
    public const string InvalidStatusCode = "OPEN_REQUEST_INVALID_STATUS";
    public const string OpenFailedCode = "REGISTER_OPEN_FAILED";
    public const string StartbelegRequiredCode = "STARTBELEG_REQUIRED";
    public const string MonatsbelegRequiredCode = "MONATSBELEG_REQUIRED";
    public const string ActorHasOtherOpenRegisterCode = "ACTOR_HAS_OTHER_OPEN_REGISTER";

    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ICashRegisterShiftService _shift;
    private readonly IActivityEventPublisher _activity;
    private readonly IAuditLogService _audit;
    private readonly TimeProvider _time;
    private readonly ILogger<CashRegisterOpenRequestService> _logger;

    public CashRegisterOpenRequestService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ICashRegisterShiftService shift,
        IActivityEventPublisher activity,
        IAuditLogService audit,
        TimeProvider time,
        ILogger<CashRegisterOpenRequestService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _shift = shift;
        _activity = activity;
        _audit = audit;
        _time = time;
        _logger = logger;
    }

    public async Task<CashRegisterOpenRequestMutationResult> CreateAsync(
        string requesterUserId,
        CreateCashRegisterOpenRequestBody body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requesterUserId))
            return Fail(TenantContextRequiredCode, "User is not authenticated.");

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return Fail(TenantContextRequiredCode, "Tenant context is required.");

        if (body.RegisterId == Guid.Empty)
            return Fail(RegisterNotFoundCode, "RegisterId is required.");

        var register = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == body.RegisterId && r.TenantId == tenantId.Value,
                cancellationToken);
        if (register is null)
            return Fail(RegisterNotFoundCode, "Cash register not found.");

        if (register.Status == RegisterStatus.Decommissioned
            || register.Status is RegisterStatus.Maintenance or RegisterStatus.Disabled
            || !register.IsActive)
        {
            return Fail(RegisterUnavailableCode, "Cash register is not available.");
        }

        if (CashRegisterAssignment.IsAssignedToOtherUser(requesterUserId, register.AssignedUserId))
            return Fail(RegisterAssignedToOtherCode, "Cash register is assigned to another user.");

        if (register.Status == RegisterStatus.Open)
        {
            if (string.Equals(register.CurrentUserId, requesterUserId, StringComparison.Ordinal))
                return Fail(RegisterAlreadyOpenCode, "Cash register is already open.");

            return Fail(RegisterHeldByOtherCode, "Cash register is already open by another user.");
        }

        var existing = await _db.CashRegisterOpenRequests
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId.Value
                     && r.CashRegisterId == body.RegisterId
                     && r.RequestedByUserId == requesterUserId
                     && r.Status == CashRegisterOpenRequestStatuses.Pending,
                cancellationToken);
        if (existing is not null)
        {
            return new CashRegisterOpenRequestMutationResult
            {
                Succeeded = true,
                Code = AlreadyPendingCode,
                Request = await MapAsync(existing, cancellationToken),
            };
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entity = new CashRegisterOpenRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            CashRegisterId = body.RegisterId,
            Status = CashRegisterOpenRequestStatuses.Pending,
            RequestedByUserId = requesterUserId,
            RequestedAt = now,
            Note = TruncateNote(body.Note),
        };

        _db.CashRegisterOpenRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await PublishActivityAsync(
            tenantId.Value,
            ActivityEventType.CashRegisterOpenRequested,
            entity,
            requesterUserId,
            $"cash-register-open-request:{entity.Id}",
            cancellationToken);

        await _audit.LogSystemOperationAsync(
            "CashRegisterOpenRequested",
            "cash_register_open_request",
            requesterUserId,
            Roles.FallbackUnknown,
            description: $"Cashier requested opening of register {register.RegisterNumber}",
            actionType: AuditEventType.CashRegisterOpenRequested,
            entityId: entity.Id,
            tenantId: tenantId.Value,
            requestData: new { entity.CashRegisterId, register.RegisterNumber });

        _logger.LogInformation(
            "Cash register open request {RequestId} created by {UserId} for register {RegisterId}",
            entity.Id,
            requesterUserId,
            body.RegisterId);

        return new CashRegisterOpenRequestMutationResult
        {
            Succeeded = true,
            Request = await MapAsync(entity, cancellationToken),
        };
    }

    public async Task<IReadOnlyList<CashRegisterOpenRequestDto>> ListMineAsync(
        string requesterUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.CashRegisterOpenRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == requesterUserId)
            .OrderByDescending(r => r.RequestedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegisterOpenRequestDto>> ListAsync(
        string? status,
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CashRegisterOpenRequest> query = _db.CashRegisterOpenRequests.AsNoTracking();
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();

        if (!string.IsNullOrWhiteSpace(status) && CashRegisterOpenRequestStatuses.IsValid(status))
        {
            var normalized = status.Trim();
            query = query.Where(r => r.Status == normalized);
        }

        if (actorIsSuperAdmin && tenantIdFilter.HasValue)
            query = query.Where(r => r.TenantId == tenantIdFilter.Value);

        var rows = await query
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<CashRegisterOpenRequestMutationResult> ApproveAsync(
        Guid requestId,
        string resolverUserId,
        string resolverRole,
        bool actorIsSuperAdmin,
        ResolveCashRegisterOpenRequestBody? body,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadForResolveAsync(requestId, actorIsSuperAdmin, cancellationToken);
        if (entity is null)
            return Fail(RequestNotFoundCode, "Open request not found.");

        if (!string.Equals(entity.Status, CashRegisterOpenRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Fail(InvalidStatusCode, "Only pending requests can be approved.");

        var register = await LoadRegisterForOpenAsync(entity, actorIsSuperAdmin, cancellationToken);
        if (register is null)
            return Fail(RegisterNotFoundCode, "Cash register not found.");

        var previousTenantId = _tenantAccessor.TenantId;
        if (actorIsSuperAdmin)
            _tenantAccessor.TenantId = entity.TenantId;

        CashRegisterOpenResult openResult;
        try
        {
            openResult = await _shift.TryOpenCashRegisterAsync(
                entity.CashRegisterId,
                entity.RequestedByUserId,
                register.StartingBalance,
                "Approved cash register open request",
                allowIdempotentSameUser: true,
                cancellationToken);
        }
        finally
        {
            if (actorIsSuperAdmin)
                _tenantAccessor.TenantId = previousTenantId;
        }

        switch (openResult.Kind)
        {
            case CashRegisterOpenKind.SuccessOpened:
            case CashRegisterOpenKind.SuccessIdempotentAlreadyOpen:
                break;
            case CashRegisterOpenKind.FailedNotFound:
                return Fail(RegisterNotFoundCode, "Cash register not found.");
            case CashRegisterOpenKind.FailedConflictOtherUser:
                return Fail(RegisterHeldByOtherCode, "Cash register is already open by another user.");
            case CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister:
                return Fail(
                    ActorHasOtherOpenRegisterCode,
                    "The requesting cashier already has another open cash register.");
            case CashRegisterOpenKind.FailedStartbelegRequired:
                return Fail(StartbelegRequiredCode, "Startbeleg must be created before this register can be opened.");
            case CashRegisterOpenKind.FailedMonatsbelegRequired:
                return Fail(MonatsbelegRequiredCode, "Monatsbeleg must be created before this register can be opened.");
            default:
                return Fail(OpenFailedCode, "Cash register could not be opened.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entity.Status = CashRegisterOpenRequestStatuses.Approved;
        entity.ResolvedByUserId = resolverUserId;
        entity.ResolvedAt = now;
        entity.ResolutionNote = TruncateNote(body?.Note);
        await _db.SaveChangesAsync(cancellationToken);

        await PublishActivityAsync(
            entity.TenantId,
            ActivityEventType.CashRegisterOpenRequestApproved,
            entity,
            resolverUserId,
            $"cash-register-open-request-approved:{entity.Id}",
            cancellationToken);

        await _audit.LogSystemOperationAsync(
            "CashRegisterOpenRequestApproved",
            "cash_register_open_request",
            resolverUserId,
            resolverRole,
            description: $"Open request approved for register {register.RegisterNumber}",
            actionType: AuditEventType.CashRegisterOpenRequestApproved,
            entityId: entity.Id,
            tenantId: entity.TenantId,
            newValues: new { entity.Status, entity.ResolvedByUserId });

        return new CashRegisterOpenRequestMutationResult
        {
            Succeeded = true,
            Request = await MapAsync(entity, cancellationToken),
        };
    }

    public async Task<CashRegisterOpenRequestMutationResult> DenyAsync(
        Guid requestId,
        string resolverUserId,
        string resolverRole,
        bool actorIsSuperAdmin,
        ResolveCashRegisterOpenRequestBody? body,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadForResolveAsync(requestId, actorIsSuperAdmin, cancellationToken);
        if (entity is null)
            return Fail(RequestNotFoundCode, "Open request not found.");

        if (!string.Equals(entity.Status, CashRegisterOpenRequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            return Fail(InvalidStatusCode, "Only pending requests can be denied.");

        entity.Status = CashRegisterOpenRequestStatuses.Denied;
        entity.ResolvedByUserId = resolverUserId;
        entity.ResolvedAt = _time.GetUtcNow().UtcDateTime;
        entity.ResolutionNote = TruncateNote(body?.Note);
        await _db.SaveChangesAsync(cancellationToken);

        await PublishActivityAsync(
            entity.TenantId,
            ActivityEventType.CashRegisterOpenRequestDenied,
            entity,
            resolverUserId,
            $"cash-register-open-request-denied:{entity.Id}",
            cancellationToken);

        await _audit.LogSystemOperationAsync(
            "CashRegisterOpenRequestDenied",
            "cash_register_open_request",
            resolverUserId,
            resolverRole,
            description: "Cash register open request denied",
            actionType: AuditEventType.CashRegisterOpenRequestDenied,
            entityId: entity.Id,
            tenantId: entity.TenantId,
            newValues: new { entity.Status, entity.ResolvedByUserId });

        return new CashRegisterOpenRequestMutationResult
        {
            Succeeded = true,
            Request = await MapAsync(entity, cancellationToken),
        };
    }

    private async Task<CashRegisterOpenRequest?> LoadForResolveAsync(
        Guid requestId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        IQueryable<CashRegisterOpenRequest> query = _db.CashRegisterOpenRequests;
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();
        return await query.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
    }

    private async Task<CashRegister?> LoadRegisterForOpenAsync(
        CashRegisterOpenRequest entity,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        IQueryable<CashRegister> query = _db.CashRegisters.AsNoTracking();
        if (actorIsSuperAdmin)
            query = query.IgnoreQueryFilters();
        return await query.FirstOrDefaultAsync(
            r => r.Id == entity.CashRegisterId && r.TenantId == entity.TenantId,
            cancellationToken);
    }

    private async Task PublishActivityAsync(
        Guid tenantId,
        ActivityEventType type,
        CashRegisterOpenRequest entity,
        string? actorUserId,
        string dedupKey,
        CancellationToken cancellationToken)
    {
        await _activity.TryPublishAsync(
            tenantId,
            type,
            new
            {
                RequestId = entity.Id.ToString(),
                CashRegisterId = entity.CashRegisterId.ToString(),
                entity.RequestedByUserId,
                entity.Status,
                Message = $"Cash register open request {entity.Status}",
            },
            actorUserId,
            dedupKey,
            cancellationToken);
    }

    private async Task<IReadOnlyList<CashRegisterOpenRequestDto>> MapManyAsync(
        IReadOnlyList<CashRegisterOpenRequest> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return Array.Empty<CashRegisterOpenRequestDto>();

        var userIds = rows.Select(r => r.RequestedByUserId).Distinct().ToList();
        var registerIds = rows.Select(r => r.CashRegisterId).Distinct().ToList();
        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();

        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.UserName, cancellationToken);

        var registers = await _db.CashRegisters.AsNoTracking().IgnoreQueryFilters()
            .Where(r => registerIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RegisterNumber, r.Location })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        return rows.Select(r =>
        {
            names.TryGetValue(r.RequestedByUserId, out var userName);
            registers.TryGetValue(r.CashRegisterId, out var register);
            tenants.TryGetValue(r.TenantId, out var tenant);
            return Map(r, userName, register?.RegisterNumber, register?.Location, tenant?.Name, tenant?.Slug);
        }).ToList();
    }

    private Task<CashRegisterOpenRequestDto> MapAsync(
        CashRegisterOpenRequest entity,
        CancellationToken cancellationToken) =>
        MapManyAsync(new[] { entity }, cancellationToken)
            .ContinueWith(t => t.Result[0], cancellationToken);

    private static CashRegisterOpenRequestDto Map(
        CashRegisterOpenRequest entity,
        string? userName,
        string? registerNumber,
        string? location,
        string? tenantName,
        string? tenantSlug) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        TenantName = tenantName,
        TenantSlug = tenantSlug,
        CashRegisterId = entity.CashRegisterId,
        RegisterNumber = registerNumber ?? string.Empty,
        Location = location,
        Status = entity.Status,
        RequestedByUserId = entity.RequestedByUserId,
        RequestedByUserName = userName,
        RequestedAt = entity.RequestedAt,
        Note = entity.Note,
        ResolvedByUserId = entity.ResolvedByUserId,
        ResolvedAt = entity.ResolvedAt,
        ResolutionNote = entity.ResolutionNote,
    };

    private static string? TruncateNote(string? note)
    {
        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static CashRegisterOpenRequestMutationResult Fail(string code, string error) => new()
    {
        Succeeded = false,
        Code = code,
        Error = error,
    };
}
