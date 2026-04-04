using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Implements ensure-ready session DTO and optional auto-open. Payment requests do not call this type; see <see cref="CashRegisterResolutionService.ValidatePaymentRegisterAsync"/>.
/// </summary>
public sealed class PosCashRegisterReadinessService : IPosCashRegisterReadinessService
{
    private readonly AppDbContext _context;
    private readonly ICashRegisterResolutionService _resolution;
    private readonly ICashRegisterShiftService _shift;
    private readonly IOptions<PosCashRegisterFeatureOptions> _options;
    private readonly ILogger<PosCashRegisterReadinessService> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public PosCashRegisterReadinessService(
        AppDbContext context,
        ICashRegisterResolutionService resolution,
        ICashRegisterShiftService shift,
        IOptions<PosCashRegisterFeatureOptions> options,
        ILogger<PosCashRegisterReadinessService> logger,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _resolution = resolution;
        _shift = shift;
        _options = options;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
    }

    /// <inheritdoc />
    public async Task<PosCashRegisterContextDto> EnsureReadyForPosAsync(
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.Value;

        if (!opts.EffectiveDefaultOnPosEntry)
            return await BuildReadOnlyContextAsync(userId, cancellationToken);

        var userSettings = await UserSettingsBootstrap.GetOrCreateTrackedUserSettingsAsync(
            _context,
            userId,
            cancellationToken);

        await _resolution.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(
            userSettings,
            userId,
            cancellationToken);

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var registers = await _context.CashRegisters
            .Include(r => r.CurrentUser)
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var operationalRegisterCount = CashRegisterPosOperationalCardinality.CountOperationalRegisters(registers);

        var (effectiveRegister, resolution) = ResolveEffectiveRegister(userSettings, registers);

        if (registers.Count == 0 || operationalRegisterCount == 0)
        {
            return StampPreferred(new PosCashRegisterContextDto
            {
                EffectiveRegisterId = null,
                Resolution = "none",
                RegisterStatus = null,
                AutoOpened = false,
                NextAction = "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            }, userSettings);
        }

        if (effectiveRegister == null)
        {
            return StampPreferred(new PosCashRegisterContextDto
            {
                EffectiveRegisterId = null,
                Resolution = "none",
                RegisterStatus = null,
                AutoOpened = false,
                NextAction = operationalRegisterCount > 1 ? "select_register" : "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            }, userSettings);
        }

        var dto = new PosCashRegisterContextDto
        {
            EffectiveRegisterId = effectiveRegister.Id.ToString(),
            Resolution = resolution,
            RegisterStatus = MapRegisterStatus(effectiveRegister.Status),
            AutoOpened = false,
            NextAction = "ready",
            MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady
        };

        if (effectiveRegister.Status == RegisterStatus.Open)
        {
            // Same occupancy predicate as payment / picker / sole auto-assign (<see cref="CashRegisterShiftOccupancy.IsHeldByOtherUser"/>).
            // AppPermissions.CashRegisterView does not relax this path (it only widens manual assignment API and closed-register auto-open eligibility).
            if (CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, effectiveRegister.CurrentUserId))
            {
                dto.NextAction = "forbidden";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
                return StampPreferred(dto, userSettings);
            }

            dto.NextAction = "ready";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady;
            return StampPreferred(dto, userSettings);
        }

        if (effectiveRegister.Status != RegisterStatus.Closed)
        {
            dto.NextAction = "open_register";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
            return StampPreferred(dto, userSettings);
        }

        var hasShiftOpen = PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.ShiftOpen);
        if (!hasShiftOpen)
        {
            dto.NextAction = "open_register";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterForbidden;
            return StampPreferred(dto, userSettings);
        }

        var isSingleOperational = CashRegisterPosOperationalCardinality.IsSingleOperationalRegisterMode(registers);
        var singleOperational = CashRegisterPosOperationalCardinality.GetSingleOperationalRegisterOrNull(registers);
        var soleEligible = opts.AutoOpenSoleClosedRegister &&
                           isSingleOperational &&
                           singleOperational != null &&
                           effectiveRegister.Id == singleOperational.Id;

        var assignedEligible = opts.AutoOpenAssignedClosedRegister &&
                               !isSingleOperational &&
                               string.Equals(resolution, "settings", StringComparison.OrdinalIgnoreCase) &&
                               PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.CashRegisterView);

        if (!soleEligible && !assignedEligible)
        {
            dto.NextAction = "open_register";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
            return StampPreferred(dto, userSettings);
        }

        if (CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, effectiveRegister.CurrentUserId))
        {
            dto.NextAction = "forbidden";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
            return StampPreferred(dto, userSettings);
        }

        var openResult = await _shift.TryOpenCashRegisterAsync(
            effectiveRegister.Id,
            userId,
            opts.DefaultAutoOpenOpeningBalance,
            soleEligible
                ? "POS auto-open (sole register)"
                : "POS auto-open (assigned default)",
            allowIdempotentSameUser: true,
            cancellationToken);

        switch (openResult.Kind)
        {
            case CashRegisterOpenKind.SuccessOpened:
                dto.AutoOpened = true;
                dto.RegisterStatus = "Open";
                dto.NextAction = "ready";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterAutoOpened;
                await PersistAssignmentAfterOpenAsync(userSettings, effectiveRegister.Id, userId, cancellationToken);
                _logger.LogInformation(
                    "POS auto-opened cash register {RegisterId} for user {UserId} ({Mode})",
                    effectiveRegister.Id,
                    userId,
                    soleEligible ? "sole" : "assigned");
                return StampPreferred(dto, userSettings);

            case CashRegisterOpenKind.SuccessIdempotentAlreadyOpen:
                dto.AutoOpened = false;
                dto.RegisterStatus = "Open";
                dto.NextAction = "ready";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady;
                await PersistAssignmentAfterOpenAsync(userSettings, effectiveRegister.Id, userId, cancellationToken);
                return StampPreferred(dto, userSettings);

            case CashRegisterOpenKind.FailedConflictOtherUser:
                dto.NextAction = "forbidden";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
                return StampPreferred(dto, userSettings);

            case CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister:
                dto.NextAction = "forbidden";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterActorAlreadyOpenElsewhere;
                return StampPreferred(dto, userSettings);

            case CashRegisterOpenKind.FailedNotFound:
                dto.EffectiveRegisterId = null;
                dto.RegisterStatus = null;
                dto.NextAction = "none";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterNotFound;
                return StampPreferred(dto, userSettings);

            case CashRegisterOpenKind.FailedInvalidState:
            default:
                dto.NextAction = "open_register";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
                return StampPreferred(dto, userSettings);
        }
    }

    private async Task PersistAssignmentAfterOpenAsync(
        UserSettings userSettings,
        Guid registerId,
        string userId,
        CancellationToken cancellationToken)
    {
        var idStr = registerId.ToString();
        if (string.Equals(userSettings.CashRegisterId?.Trim(), idStr, StringComparison.OrdinalIgnoreCase))
            return;

        userSettings.CashRegisterId = idStr;
        userSettings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Persisted cash register assignment {RegisterId} for user {UserId} after POS open", registerId, userId);
    }

    private async Task<PosCashRegisterContextDto> BuildReadOnlyContextAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var userSettings = await _context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(us => us.UserId == userId, cancellationToken);

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var registers = await _context.CashRegisters
            .AsNoTracking()
            .Include(r => r.CurrentUser)
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var operationalRegisterCount = CashRegisterPosOperationalCardinality.CountOperationalRegisters(registers);

        if (registers.Count == 0 || operationalRegisterCount == 0)
        {
            return StampPreferred(new PosCashRegisterContextDto
            {
                Resolution = "none",
                NextAction = "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            }, userSettings);
        }

        var (effective, resolution) = ResolveEffectiveRegister(userSettings, registers);
        if (effective == null)
        {
            return StampPreferred(new PosCashRegisterContextDto
            {
                Resolution = "none",
                NextAction = operationalRegisterCount > 1 ? "select_register" : "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            }, userSettings);
        }

        var dto = new PosCashRegisterContextDto
        {
            EffectiveRegisterId = effective.Id.ToString(),
            Resolution = resolution,
            RegisterStatus = MapRegisterStatus(effective.Status),
            AutoOpened = false,
            MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady
        };

        if (effective.Status == RegisterStatus.Open &&
            CashRegisterShiftOccupancy.IsHeldByOtherUser(userId, effective.CurrentUserId))
        {
            dto.NextAction = "forbidden";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
            return StampPreferred(dto, userSettings);
        }

        if (effective.Status == RegisterStatus.Open)
        {
            dto.NextAction = "ready";
            return StampPreferred(dto, userSettings);
        }

        dto.NextAction = "open_register";
        dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
        return StampPreferred(dto, userSettings);
    }

    private static PosCashRegisterContextDto StampPreferred(PosCashRegisterContextDto dto, UserSettings? userSettings)
    {
        dto.PreferredRegisterId = NormalizePersistedPreferenceRegisterId(userSettings?.CashRegisterId);
        return dto;
    }

    /// <summary>
    /// Normalized echo of <see cref="UserSettings.CashRegisterId"/>; null when unset or not a non-empty GUID string.
    /// </summary>
    private static string? NormalizePersistedPreferenceRegisterId(string? raw)
    {
        var t = raw?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (!Guid.TryParse(t, out var g) || g == Guid.Empty) return null;
        return g.ToString("D");
    }

    private static (CashRegister? Register, string Resolution) ResolveEffectiveRegister(
        UserSettings? userSettings,
        List<CashRegister> registers)
    {
        CashRegister? effective = null;
        var resolution = "none";

        var assignedRaw = userSettings?.CashRegisterId?.Trim();
        if (!string.IsNullOrEmpty(assignedRaw) &&
            Guid.TryParse(assignedRaw, out var ag) &&
            ag != Guid.Empty)
        {
            effective = registers.FirstOrDefault(r => r.Id == ag);
            if (effective != null)
                resolution = "settings";
        }

        if (effective == null)
        {
            var singleOperational = CashRegisterPosOperationalCardinality.GetSingleOperationalRegisterOrNull(registers);
            if (singleOperational != null)
            {
                effective = singleOperational;
                resolution = "sole_register";
            }
        }

        return (effective, resolution);
    }

    private static string? MapRegisterStatus(RegisterStatus status) =>
        status switch
        {
            RegisterStatus.Open => "Open",
            RegisterStatus.Closed => "Closed",
            RegisterStatus.Maintenance => "Maintenance",
            RegisterStatus.Disabled => "Disabled",
            _ => null
        };
}
