using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>Creates tenant-scoped cash register inventory rows with duplicate checks and audit.</summary>
public sealed class CashRegisterManagementService : ICashRegisterManagementService
{
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<CashRegisterManagementService> _logger;

    public CashRegisterManagementService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService auditLog,
        ILogger<CashRegisterManagementService> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<CashRegister> CreateAsync(
        CreateCashRegisterRequest request,
        string actorUserId,
        string actorRole,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var registerNumber = request.RegisterNumber.Trim();
        var location = request.Location.Trim();
        if (string.IsNullOrEmpty(registerNumber))
            throw new ArgumentException("RegisterNumber is required.", nameof(request));
        if (string.IsNullOrEmpty(location))
            throw new ArgumentException("Location is required.", nameof(request));

        var tenantId = await ResolveTargetTenantIdAsync(request, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        var exists = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                r => r.TenantId == tenantId && r.RegisterNumber == registerNumber,
                cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Register number {registerNumber} already exists for this tenant.");
        }

        var now = DateTime.UtcNow;
        var register = new CashRegister
        {
            TenantId = tenantId,
            RegisterNumber = registerNumber,
            Location = location,
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Closed,
            CreatedAt = now,
            CreatedBy = actorUserId,
            IsActive = true,
        };

        _db.CashRegisters.Add(register);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditCreateAsync(register, actorUserId, actorRole).ConfigureAwait(false);

        _logger.LogInformation(
            "Cash register created RegisterId={RegisterId} RegisterNumber={RegisterNumber} TenantId={TenantId} Actor={Actor}",
            register.Id,
            register.RegisterNumber,
            register.TenantId,
            actorUserId);

        return register;
    }

    public async Task<PagedResult<CashRegisterDto>> ListAsync(
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var tenantId = await ResolveListTenantIdAsync(tenantIdFilter, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        var query = _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.RegisterNumber);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<CashRegisterDto>
        {
            Items = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };
    }

    public async Task<CashRegisterDto> UpdateAsync(
        Guid id,
        UpdateCashRegisterRequest request,
        string actorUserId,
        string actorRole,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var registerNumber = request.RegisterNumber.Trim();
        var location = request.Location.Trim();
        if (string.IsNullOrEmpty(registerNumber))
            throw new ArgumentException("RegisterNumber is required.", nameof(request));
        if (string.IsNullOrEmpty(location))
            throw new ArgumentException("Location is required.", nameof(request));

        var register = await _db.CashRegisters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (register == null)
            throw new InvalidOperationException("Cash register not found.");

        await EnsureTenantAccessAsync(register.TenantId, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (register.Status == RegisterStatus.Decommissioned)
            throw new InvalidOperationException("Decommissioned cash registers cannot be updated.");

        var duplicate = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                r => r.TenantId == register.TenantId
                     && r.RegisterNumber == registerNumber
                     && r.Id != id,
                cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            throw new InvalidOperationException(
                $"Register number {registerNumber} already exists for this tenant.");
        }

        var oldValues = new
        {
            register.RegisterNumber,
            register.Location,
        };

        register.RegisterNumber = registerNumber;
        register.Location = location;
        register.UpdatedAt = DateTime.UtcNow;
        register.UpdatedBy = actorUserId;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditUpdateAsync(register, actorUserId, actorRole, oldValues).ConfigureAwait(false);

        _logger.LogInformation(
            "Cash register updated RegisterId={RegisterId} RegisterNumber={RegisterNumber} TenantId={TenantId} Actor={Actor}",
            register.Id,
            register.RegisterNumber,
            register.TenantId,
            actorUserId);

        return MapToDto(register);
    }

    private async Task<Guid> ResolveListTenantIdAsync(
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        if (tenantIdFilter is Guid requestedTenantId && requestedTenantId != Guid.Empty)
        {
            if (!actorIsSuperAdmin)
            {
                var effectiveTenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (effectiveTenantId != requestedTenantId)
                {
                    throw new InvalidOperationException("TenantId filter is only allowed for SuperAdmin.");
                }
            }

            var tenantExists = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.Id == requestedTenantId && t.DeletedAtUtc == null, cancellationToken)
                .ConfigureAwait(false);
            if (!tenantExists)
                throw new InvalidOperationException("Tenant not found.");

            return requestedTenantId;
        }

        try
        {
            return await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve tenant for cash register list");
            throw new UnauthorizedAccessException("Tenant context required.", ex);
        }
    }

    private async Task EnsureTenantAccessAsync(
        Guid registerTenantId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        if (actorIsSuperAdmin)
            return;

        var effectiveTenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (effectiveTenantId != registerTenantId)
            throw new InvalidOperationException("Cash register not found.");
    }

    private async Task<Guid> ResolveTargetTenantIdAsync(
        CreateCashRegisterRequest request,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        if (request.TenantId is Guid requestedTenantId && requestedTenantId != Guid.Empty)
        {
            if (!actorIsSuperAdmin)
            {
                throw new InvalidOperationException("TenantId is only allowed for SuperAdmin.");
            }

            var tenantExists = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.Id == requestedTenantId && t.DeletedAtUtc == null, cancellationToken)
                .ConfigureAwait(false);
            if (!tenantExists)
                throw new InvalidOperationException("Tenant not found.");

            return requestedTenantId;
        }

        try
        {
            return await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve tenant for cash register create");
            throw new UnauthorizedAccessException("Tenant context required.", ex);
        }
    }

    private async Task TryAuditCreateAsync(CashRegister register, string actorUserId, string actorRole)
    {
        try
        {
            await _auditLog.LogEntityChangeAsync(
                AuditLogActions.CASH_REGISTER_CREATED,
                AuditLogEntityTypes.CASH_REGISTER,
                register.Id,
                actorUserId,
                actorRole,
                oldValues: null,
                newValues: new
                {
                    register.RegisterNumber,
                    register.Location,
                    register.TenantId,
                    status = register.Status.ToString(),
                },
                description: $"Cash register {register.RegisterNumber} created.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for cash register create RegisterId={RegisterId}", register.Id);
        }
    }

    private async Task TryAuditUpdateAsync(
        CashRegister register,
        string actorUserId,
        string actorRole,
        object oldValues)
    {
        try
        {
            await _auditLog.LogEntityChangeAsync(
                AuditLogActions.CASH_REGISTER_UPDATED,
                AuditLogEntityTypes.CASH_REGISTER,
                register.Id,
                actorUserId,
                actorRole,
                oldValues: oldValues,
                newValues: new
                {
                    register.RegisterNumber,
                    register.Location,
                },
                description: $"Cash register {register.RegisterNumber} updated.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for cash register update RegisterId={RegisterId}", register.Id);
        }
    }

    private static CashRegisterDto MapToDto(CashRegister register) =>
        new()
        {
            Id = register.Id,
            TenantId = register.TenantId,
            RegisterNumber = register.RegisterNumber,
            Location = register.Location,
            Status = register.Status,
            StartingBalance = register.StartingBalance,
            CurrentBalance = register.CurrentBalance,
            LastBalanceUpdate = register.LastBalanceUpdate,
            CurrentUserId = register.CurrentUserId,
            IsActive = register.IsActive,
            DecommissionedAtUtc = register.DecommissionedAtUtc,
            DecommissionReason = register.DecommissionReason,
            CreatedAt = register.CreatedAt,
            CreatedBy = register.CreatedBy,
            UpdatedAt = register.UpdatedAt,
            UpdatedBy = register.UpdatedBy,
        };
}
