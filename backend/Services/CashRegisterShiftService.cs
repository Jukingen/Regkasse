using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
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

    public CashRegisterShiftService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CashRegisterShiftService> logger,
        ISettingsTenantResolver settingsTenantResolver,
        IRksvStartbelegPolicy rksvStartbelegPolicy,
        IRksvMonatsbelegPolicy rksvMonatsbelegPolicy)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
        _rksvStartbelegPolicy = rksvStartbelegPolicy;
        _rksvMonatsbelegPolicy = rksvMonatsbelegPolicy;
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
                        && string.Equals(r.CurrentUserId, shiftOperatorUserId, StringComparison.Ordinal),
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

            if (register.Status == RegisterStatus.Closed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.AlreadyClosed();
            }

            if (register.Status == RegisterStatus.Decommissioned)
            {
                await transaction.RollbackAsync(cancellationToken);
                return CashRegisterCloseResult.AlreadyClosed();
            }

            if (string.IsNullOrEmpty(shiftOperatorUserId) ||
                !string.Equals(register.CurrentUserId, shiftOperatorUserId, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
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
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Cash register {RegisterId} closed by user {UserId}", registerId, shiftOperatorUserId);
            return CashRegisterCloseResult.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "TryCloseCashRegisterAsync failed for register {RegisterId}", registerId);
            throw;
        }
    }
}
