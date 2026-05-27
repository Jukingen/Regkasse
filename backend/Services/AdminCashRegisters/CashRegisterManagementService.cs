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
    private readonly ICashRegisterListEnrichmentService _enrichment;
    private readonly ILogger<CashRegisterManagementService> _logger;

    public CashRegisterManagementService(
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService auditLog,
        ICashRegisterListEnrichmentService enrichment,
        ILogger<CashRegisterManagementService> logger)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _auditLog = auditLog;
        _enrichment = enrichment;
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
        string? excludeStatus,
        bool actorIsSuperAdmin,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = await BuildAuthorizedListQueryAsync(tenantIdFilter, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);
        query = ApplyExcludeStatusFilter(query, excludeStatus);

        var ordered = query
            .OrderBy(r => r.Tenant != null ? r.Tenant.Name : string.Empty)
            .ThenBy(r => r.RegisterNumber);
        var totalCount = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dtos = items.Select(MapToDto).ToList();
        await _enrichment.ApplyAsync(dtos, items, cancellationToken).ConfigureAwait(false);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<CashRegisterDto>
        {
            Items = dtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
        };
    }

    public async Task<CashRegisterDto?> GetByIdAsync(
        Guid cashRegisterId,
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return null;

        var query = await BuildAuthorizedListQueryAsync(tenantIdFilter, actorIsSuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        var register = await query.FirstOrDefaultAsync(r => r.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        if (register == null)
            return null;

        var dto = MapToDto(register);
        await _enrichment.ApplyAsync([dto], [register], cancellationToken).ConfigureAwait(false);
        return dto;
    }

    public async Task<int> GetActiveCountForTenantAsync(
        Guid tenantId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTenantAsync(tenantId, actorIsSuperAdmin, cancellationToken).ConfigureAwait(false);

        return await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(
                r => r.TenantId == tenantId && r.Status != RegisterStatus.Decommissioned,
                cancellationToken)
            .ConfigureAwait(false);
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

        var dto = MapToDto(register);
        await _enrichment.ApplyAsync([dto], [register], cancellationToken).ConfigureAwait(false);
        return dto;
    }

    private async Task<IQueryable<CashRegister>> BuildAuthorizedListQueryAsync(
        Guid? tenantIdFilter,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        var query = _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(r => r.Tenant)
            .Include(r => r.CurrentUser);

        if (actorIsSuperAdmin)
        {
            if (tenantIdFilter is Guid requestedTenantId && requestedTenantId != Guid.Empty)
            {
                var tenantExists = await _db.Tenants.AsNoTracking()
                    .AnyAsync(t => t.Id == requestedTenantId && t.DeletedAtUtc == null, cancellationToken)
                    .ConfigureAwait(false);
                if (!tenantExists)
                    throw new InvalidOperationException("Tenant not found.");

                return query.Where(r => r.TenantId == requestedTenantId);
            }

            return query;
        }

        Guid effectiveTenantId;
        try
        {
            effectiveTenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve tenant for cash register list");
            throw new UnauthorizedAccessException("Tenant context required.", ex);
        }

        if (tenantIdFilter is Guid requested && requested != Guid.Empty && requested != effectiveTenantId)
        {
            throw new UnauthorizedAccessException("Cannot access cash registers of other tenants.");
        }

        return query.Where(r => r.TenantId == effectiveTenantId);
    }

    private static IQueryable<CashRegister> ApplyExcludeStatusFilter(
        IQueryable<CashRegister> query,
        string? excludeStatus)
    {
        if (string.IsNullOrWhiteSpace(excludeStatus))
            return query;

        var normalized = excludeStatus.Trim();

        if (Enum.TryParse<RegisterStatus>(normalized, ignoreCase: true, out var parsedStatus))
        {
            return query.Where(r => r.Status != parsedStatus);
        }

        if (int.TryParse(normalized, out var numericStatus)
            && Enum.IsDefined(typeof(RegisterStatus), numericStatus))
        {
            var parsedNumericStatus = (RegisterStatus)numericStatus;
            return query.Where(r => r.Status != parsedNumericStatus);
        }

        throw new ArgumentException($"Unknown register status '{excludeStatus}'.", nameof(excludeStatus));
    }

    private async Task EnsureCanAccessTenantAsync(
        Guid tenantId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant not found.");

        var tenantExists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            throw new InvalidOperationException("Tenant not found.");

        if (actorIsSuperAdmin)
            return;

        Guid effectiveTenantId;
        try
        {
            effectiveTenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve tenant for cash register tenant-scoped operation");
            throw new UnauthorizedAccessException("Tenant context required.", ex);
        }

        if (effectiveTenantId != tenantId)
            throw new UnauthorizedAccessException("Cannot access cash registers of other tenants.");
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
            TenantName = register.Tenant?.Name,
            TenantSlug = register.Tenant?.Slug,
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
