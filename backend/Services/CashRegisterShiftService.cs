using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class CashRegisterShiftService : ICashRegisterShiftService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CashRegisterShiftService> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly IRksvStartbelegPolicy _rksvStartbelegPolicy;
    private readonly IRksvMonatsbelegPolicy _rksvMonatsbelegPolicy;
    private readonly ActivityEventRecorder? _activityEvents;

    public CashRegisterShiftService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CashRegisterShiftService> logger,
        ISettingsTenantResolver settingsTenantResolver,
        IRksvStartbelegPolicy rksvStartbelegPolicy,
        IRksvMonatsbelegPolicy rksvMonatsbelegPolicy,
        ActivityEventRecorder? activityEvents = null)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
        _rksvStartbelegPolicy = rksvStartbelegPolicy;
        _rksvMonatsbelegPolicy = rksvMonatsbelegPolicy;
        _activityEvents = activityEvents;
    }

    /// <inheritdoc />
    public async Task<CashRegisterOpenResult> TryOpenCashRegisterAsync(
        Guid registerId,
        string shiftOperatorUserId,
        decimal openingBalance,
        string transactionDescription,
        bool allowIdempotentSameUser,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            await CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync(_context, registerId, cancellationToken);

            var register = await _context.CashRegisters
                .Include(r => r.CurrentUser)
                .FirstOrDefaultAsync(r => r.Id == registerId && r.TenantId == tenantId, cancellationToken);

            if (register == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterOpenResult.NotFound();
            }

            if (register.Status == RegisterStatus.Open)
            {
                if (string.Equals(register.CurrentUserId, shiftOperatorUserId, StringComparison.Ordinal))
                {
                    if (allowIdempotentSameUser)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return CashRegisterOpenResult.IdempotentSameUser(register.RegisterNumber);
                    }

                    await transaction.RollbackAsync(cancellationToken);
                    return CashRegisterOpenResult.AlreadyOpenSameUserNonIdempotent();
                }

                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterOpenResult.ConflictOtherUser();
            }

            if (register.Status != RegisterStatus.Closed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterOpenResult.InvalidState();
            }

            if (!string.IsNullOrEmpty(register.CurrentUserId) &&
                !string.Equals(register.CurrentUserId, shiftOperatorUserId, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterOpenResult.ConflictOtherUser();
            }

            var user = await _userManager.FindByIdAsync(shiftOperatorUserId);
            if (user == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning("Open cash register: shift operator user {UserId} not found", shiftOperatorUserId);
                return CashRegisterOpenResult.NotFound();
            }

            var actorHasOtherOpenRegister = await _context.CashRegisters
                .AsNoTracking()
                .AnyAsync(
                    r => r.TenantId == tenantId
                        && r.Id != registerId
                        && r.Status == RegisterStatus.Open
                        && r.CurrentUserId != null
                        && r.CurrentUserId == shiftOperatorUserId,
                    cancellationToken);

            if (actorHasOtherOpenRegister)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    "Open cash register {RegisterId} rejected: user {UserId} already has another open register",
                    registerId,
                    shiftOperatorUserId);
                return CashRegisterOpenResult.ActorAlreadyHasOtherOpenRegister();
            }

            if (_rksvStartbelegPolicy.SessionGateApplies &&
                !await _rksvStartbelegPolicy.HasStartbelegForRegisterAsync(registerId, cancellationToken)
                    .ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    "Open cash register {RegisterId} rejected: RKSV Startbeleg missing (production TSE mode)",
                    registerId);
                return CashRegisterOpenResult.StartbelegRequired();
            }

            if (_rksvMonatsbelegPolicy.SessionGateApplies)
            {
                var (y, m) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
                if (!await _rksvMonatsbelegPolicy.HasMonatsbelegForRegisterMonthAsync(registerId, y, m, cancellationToken)
                        .ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning(
                        "Open cash register {RegisterId} rejected: RKSV Monatsbeleg missing for {Year}-{Month}",
                        registerId,
                        y,
                        m);
                    return CashRegisterOpenResult.MonatsbelegRequired();
                }
            }

            register.Status = RegisterStatus.Open;
            register.CurrentUser = user;
            // Always persist shift ownership on the FK column (payment + close authorize via CurrentUserId).
            register.CurrentUserId = shiftOperatorUserId;
            register.LastBalanceUpdate = DateTime.UtcNow;
            register.UpdatedAt = DateTime.UtcNow;

            var tx = new CashRegisterTransaction
            {
                Id = Guid.NewGuid(),
                CashRegisterId = register.Id,
                TransactionType = TransactionType.Open,
                Amount = openingBalance,
                Description = transactionDescription.Length > 500
                    ? transactionDescription[..500]
                    : transactionDescription,
                UserId = shiftOperatorUserId,
                TransactionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.CashRegisterTransactions.Add(tx);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Cash register {RegisterId} opened by user {UserId} (idempotent={Idempotent})",
                registerId,
                shiftOperatorUserId,
                allowIdempotentSameUser);

            if (_activityEvents != null)
            {
                await _activityEvents.TryPublishAsync(
                    new ActivityEventPublishRequest(
                        tenantId,
                        ActivityEventType.CashRegisterOpened,
                        "Cash register opened",
                        Description: $"Register {register.RegisterNumber} was opened.",
                        ActorUserId: shiftOperatorUserId,
                        EntityType: "cash_register",
                        EntityId: registerId.ToString()),
                    cancellationToken).ConfigureAwait(false);
            }

            return CashRegisterOpenResult.Opened(register.RegisterNumber);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "TryOpenCashRegisterAsync failed for register {RegisterId}", registerId);
            throw;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Same row-lock entry as <see cref="TryOpenCashRegisterAsync"/> and payment commit authorization
    /// (<see cref="CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync"/>): evaluate invariants on the locked register row inside the transaction.
    /// </remarks>
    public async Task<CashRegisterCloseResult> TryCloseCashRegisterAsync(
        Guid registerId,
        string shiftOperatorUserId,
        decimal closingBalance,
        CancellationToken cancellationToken = default,
        bool completeActiveShifts = true)
    {
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            await CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync(_context, registerId, cancellationToken);

            var register = await _context.CashRegisters
                .Include(r => r.CurrentUser)
                .FirstOrDefaultAsync(r => r.Id == registerId && r.TenantId == tenantId, cancellationToken);

            if (register == null)
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.NotFound();
            }

            if (register.Status == RegisterStatus.Closed)
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.AlreadyClosed();
            }

            if (register.Status == RegisterStatus.Decommissioned)
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.AlreadyClosed();
            }

            if (string.IsNullOrEmpty(shiftOperatorUserId) ||
                !string.Equals(register.CurrentUserId, shiftOperatorUserId, StringComparison.Ordinal))
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.Forbidden();
            }

            register.Status = RegisterStatus.Closed;
            register.CurrentBalance = closingBalance;
            register.LastBalanceUpdate = DateTime.UtcNow;
            register.UpdatedAt = DateTime.UtcNow;
            register.CurrentUser = null;
            register.CurrentUserId = null;

            var closeTx = new CashRegisterTransaction
            {
                CashRegisterId = register.Id,
                TransactionType = TransactionType.Close,
                Amount = closingBalance,
                Description = "Kasa kapanışı",
                UserId = shiftOperatorUserId,
                TransactionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.CashRegisterTransactions.Add(closeTx);

            if (completeActiveShifts)
            {
                await CashierShiftCompletionHelper.CompleteActiveShiftsForRegisterAsync(
                    _context,
                    tenantId,
                    registerId,
                    shiftOperatorUserId,
                    "Register closed",
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (ownsTransaction)
                await transaction!.CommitAsync(cancellationToken);

            _logger.LogInformation("Cash register {RegisterId} closed by user {UserId}", registerId, shiftOperatorUserId);

            if (_activityEvents != null)
            {
                await _activityEvents.TryPublishAsync(
                    new ActivityEventPublishRequest(
                        tenantId,
                        ActivityEventType.CashRegisterClosed,
                        "Cash register closed",
                        Description: $"Register {register.RegisterNumber} was closed.",
                        ActorUserId: shiftOperatorUserId,
                        EntityType: "cash_register",
                        EntityId: registerId.ToString()),
                    cancellationToken).ConfigureAwait(false);
            }

            return CashRegisterCloseResult.Success();
        }
        catch (Exception ex)
        {
            if (ownsTransaction)
                await transaction!.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "TryCloseCashRegisterAsync failed for register {RegisterId}", registerId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CashRegisterCloseResult> TryForceCloseCashRegisterAsync(
        Guid registerId,
        string actorUserId,
        decimal closingBalance,
        string description,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            await CashRegisterDatabaseLock.AcquireRegisterRowExclusiveLockAsync(_context, registerId, cancellationToken);

            var register = await _context.CashRegisters
                .Include(r => r.CurrentUser)
                .FirstOrDefaultAsync(r => r.Id == registerId && r.TenantId == tenantId, cancellationToken);

            if (register == null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.NotFound();
            }

            if (register.Status == RegisterStatus.Closed || register.Status == RegisterStatus.Decommissioned)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.AlreadyClosed();
            }

            var previousOwnerId = register.CurrentUserId;
            var resolvedActorUserId = await ResolveForceCloseActorUserIdAsync(
                    actorUserId,
                    previousOwnerId,
                    cancellationToken)
                .ConfigureAwait(false);

            register.Status = RegisterStatus.Closed;
            register.CurrentBalance = closingBalance;
            register.LastBalanceUpdate = DateTime.UtcNow;
            register.UpdatedAt = DateTime.UtcNow;
            register.CurrentUser = null;
            register.CurrentUserId = null;

            var closeDescription = description.Length > 500 ? description[..500] : description;
            var closeTx = new CashRegisterTransaction
            {
                CashRegisterId = register.Id,
                TransactionType = TransactionType.Close,
                Amount = closingBalance,
                Description = closeDescription,
                UserId = resolvedActorUserId,
                TransactionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
            };

            _context.CashRegisterTransactions.Add(closeTx);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogWarning(
                "Cash register {RegisterId} force-closed by {ActorUserId} (resolved={ResolvedActorUserId}, previous owner {PreviousOwnerId})",
                registerId,
                actorUserId,
                resolvedActorUserId,
                previousOwnerId ?? "(none)");

            if (_activityEvents != null)
            {
                await _activityEvents.TryPublishAsync(
                    new ActivityEventPublishRequest(
                        tenantId,
                        ActivityEventType.CashRegisterClosed,
                        "Cash register force-closed",
                        Description: $"Register {register.RegisterNumber} was force-closed.",
                        ActorUserId: resolvedActorUserId,
                        EntityType: "cash_register",
                        EntityId: registerId.ToString()),
                    cancellationToken).ConfigureAwait(false);
            }

            return CashRegisterCloseResult.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                ex,
                "Failed to force-close cash register {RegisterId} for user {UserId}: {Error}",
                registerId,
                actorUserId,
                ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Resolves a real AspNetUsers id for force-close transactions.
    /// Auto-close passes "system", which is not a valid FK target.
    /// </summary>
    private async Task<string> ResolveForceCloseActorUserIdAsync(
        string? actorUserId,
        string? previousOwnerId,
        CancellationToken cancellationToken)
    {
        if (!IsPlaceholderActorUserId(actorUserId))
        {
            var actor = await _userManager.FindByIdAsync(actorUserId!).ConfigureAwait(false);
            if (actor is not null)
                return actor.Id;
        }

        if (!string.IsNullOrWhiteSpace(previousOwnerId))
        {
            var owner = await _userManager.FindByIdAsync(previousOwnerId).ConfigureAwait(false);
            if (owner is not null)
                return owner.Id;
        }

        var superAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin).ConfigureAwait(false);
        var fallback = superAdmins.FirstOrDefault(u => u.IsActive) ?? superAdmins.FirstOrDefault();
        if (fallback is not null)
        {
            _logger.LogWarning(
                "Force-close actor {ActorUserId} is invalid; using SuperAdmin {FallbackUserId}",
                actorUserId ?? "(null)",
                fallback.Id);
            return fallback.Id;
        }

        throw new InvalidOperationException(
            "Cannot force-close cash register: no valid AspNetUsers actor for cash_register_transactions.UserId.");
    }

    private static bool IsPlaceholderActorUserId(string? actorUserId) =>
        string.IsNullOrWhiteSpace(actorUserId)
        || string.Equals(actorUserId.Trim(), "system", StringComparison.OrdinalIgnoreCase);
}
