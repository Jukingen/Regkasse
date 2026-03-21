using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class PosCashRegisterReadinessService : IPosCashRegisterReadinessService
{
    private readonly AppDbContext _context;
    private readonly ICashRegisterResolutionService _resolution;
    private readonly ICashRegisterShiftService _shift;
    private readonly IOptions<PosCashRegisterFeatureOptions> _options;
    private readonly ILogger<PosCashRegisterReadinessService> _logger;

    public PosCashRegisterReadinessService(
        AppDbContext context,
        ICashRegisterResolutionService resolution,
        ICashRegisterShiftService shift,
        IOptions<PosCashRegisterFeatureOptions> options,
        ILogger<PosCashRegisterReadinessService> logger)
    {
        _context = context;
        _resolution = resolution;
        _shift = shift;
        _options = options;
        _logger = logger;
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

        var registers = await _context.CashRegisters
            .Include(r => r.CurrentUser)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var (effectiveRegister, resolution) = ResolveEffectiveRegister(userSettings, registers);

        if (registers.Count == 0)
        {
            return new PosCashRegisterContextDto
            {
                EffectiveRegisterId = null,
                Resolution = "none",
                RegisterStatus = null,
                AutoOpened = false,
                NextAction = "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            };
        }

        if (effectiveRegister == null)
        {
            return new PosCashRegisterContextDto
            {
                EffectiveRegisterId = null,
                Resolution = "none",
                RegisterStatus = null,
                AutoOpened = false,
                NextAction = registers.Count > 1 ? "select_register" : "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            };
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
            if (!string.IsNullOrEmpty(effectiveRegister.CurrentUserId) &&
                !string.Equals(effectiveRegister.CurrentUserId, userId, StringComparison.Ordinal))
            {
                dto.NextAction = "forbidden";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
                return dto;
            }

            dto.NextAction = "ready";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady;
            return dto;
        }

        if (effectiveRegister.Status != RegisterStatus.Closed)
        {
            dto.NextAction = "open_register";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
            return dto;
        }

        var hasShiftOpen = PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.ShiftOpen);
        if (!hasShiftOpen)
        {
            dto.NextAction = "open_register";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterForbidden;
            return dto;
        }

        var sole = registers.Count == 1;
        var soleEligible = opts.AutoOpenSoleClosedRegister &&
                           sole &&
                           effectiveRegister.Id == registers[0].Id;

        var assignedEligible = opts.AutoOpenAssignedClosedRegister &&
                               !sole &&
                               string.Equals(resolution, "settings", StringComparison.OrdinalIgnoreCase) &&
                               PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.CashRegisterView);

        if (!soleEligible && !assignedEligible)
        {
            dto.NextAction = "open_register";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
            return dto;
        }

        if (!string.IsNullOrEmpty(effectiveRegister.CurrentUserId) &&
            !string.Equals(effectiveRegister.CurrentUserId, userId, StringComparison.Ordinal))
        {
            dto.NextAction = "forbidden";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
            return dto;
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
                return dto;

            case CashRegisterOpenKind.SuccessIdempotentAlreadyOpen:
                dto.AutoOpened = false;
                dto.RegisterStatus = "Open";
                dto.NextAction = "ready";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterReady;
                await PersistAssignmentAfterOpenAsync(userSettings, effectiveRegister.Id, userId, cancellationToken);
                return dto;

            case CashRegisterOpenKind.FailedConflictOtherUser:
                dto.NextAction = "forbidden";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
                return dto;

            case CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister:
                dto.NextAction = "forbidden";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterActorAlreadyOpenElsewhere;
                return dto;

            case CashRegisterOpenKind.FailedNotFound:
                dto.EffectiveRegisterId = null;
                dto.RegisterStatus = null;
                dto.NextAction = "none";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterNotFound;
                return dto;

            case CashRegisterOpenKind.FailedInvalidState:
            default:
                dto.NextAction = "open_register";
                dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
                return dto;
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

        var registers = await _context.CashRegisters
            .AsNoTracking()
            .Include(r => r.CurrentUser)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        if (registers.Count == 0)
        {
            return new PosCashRegisterContextDto
            {
                Resolution = "none",
                NextAction = "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            };
        }

        var (effective, resolution) = ResolveEffectiveRegister(userSettings, registers);
        if (effective == null)
        {
            return new PosCashRegisterContextDto
            {
                Resolution = "none",
                NextAction = registers.Count > 1 ? "select_register" : "none",
                MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterRequired
            };
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
            !string.IsNullOrEmpty(effective.CurrentUserId) &&
            !string.Equals(effective.CurrentUserId, userId, StringComparison.Ordinal))
        {
            dto.NextAction = "forbidden";
            dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterConflict;
            return dto;
        }

        if (effective.Status == RegisterStatus.Open)
        {
            dto.NextAction = "ready";
            return dto;
        }

        dto.NextAction = "open_register";
        dto.MessageCode = PosCashRegisterReadinessMessageCodes.CashRegisterClosed;
        return dto;
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

        if (effective == null && registers.Count == 1)
        {
            effective = registers[0];
            resolution = "sole_register";
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
