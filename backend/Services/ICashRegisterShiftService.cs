namespace KasseAPI_Final.Services;

/// <summary>
/// Opens a cash register for a shift (shared by manual API and POS auto-open).
/// </summary>
public interface ICashRegisterShiftService
{
    /// <summary>
    /// Opens a closed register or handles idempotent / conflict cases.
    /// Uses a row lock on PostgreSQL when available to reduce double-open races.
    /// </summary>
    Task<CashRegisterOpenResult> TryOpenCashRegisterAsync(
        Guid registerId,
        string shiftOperatorUserId,
        decimal openingBalance,
        string transactionDescription,
        bool allowIdempotentSameUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes an open register for the operational shift owner. Uses the same row lock strategy as <see cref="TryOpenCashRegisterAsync"/> on PostgreSQL.
    /// </summary>
    Task<CashRegisterCloseResult> TryCloseCashRegisterAsync(
        Guid registerId,
        string shiftOperatorUserId,
        decimal closingBalance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes an open register regardless of current shift owner (Manager/Super Admin recovery).
    /// </summary>
    Task<CashRegisterCloseResult> TryForceCloseCashRegisterAsync(
        Guid registerId,
        string actorUserId,
        decimal closingBalance,
        string description,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a shift open attempt.
/// </summary>
public sealed class CashRegisterOpenResult
{
    public CashRegisterOpenKind Kind { get; init; }
    public string? RegisterNumber { get; init; }

    public static CashRegisterOpenResult Opened(string? registerNumber) =>
        new() { Kind = CashRegisterOpenKind.SuccessOpened, RegisterNumber = registerNumber };

    public static CashRegisterOpenResult IdempotentSameUser(string? registerNumber) =>
        new() { Kind = CashRegisterOpenKind.SuccessIdempotentAlreadyOpen, RegisterNumber = registerNumber };

    public static CashRegisterOpenResult NotFound() =>
        new() { Kind = CashRegisterOpenKind.FailedNotFound };

    public static CashRegisterOpenResult AlreadyOpenSameUserNonIdempotent() =>
        new() { Kind = CashRegisterOpenKind.FailedAlreadyOpenSameUserNotIdempotent };

    public static CashRegisterOpenResult ConflictOtherUser() =>
        new() { Kind = CashRegisterOpenKind.FailedConflictOtherUser };

    public static CashRegisterOpenResult InvalidState() =>
        new() { Kind = CashRegisterOpenKind.FailedInvalidState };

    /// <summary>
    /// Actor already has a different cash register open (one open register per user).
    /// </summary>
    public static CashRegisterOpenResult ActorAlreadyHasOtherOpenRegister() =>
        new() { Kind = CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister };

    public static CashRegisterOpenResult StartbelegRequired() =>
        new() { Kind = CashRegisterOpenKind.FailedStartbelegRequired };

    public static CashRegisterOpenResult MonatsbelegRequired() =>
        new() { Kind = CashRegisterOpenKind.FailedMonatsbelegRequired };
}

public enum CashRegisterOpenKind
{
    SuccessOpened,
    SuccessIdempotentAlreadyOpen,
    FailedNotFound,
    FailedAlreadyOpenSameUserNotIdempotent,
    FailedConflictOtherUser,
    FailedActorAlreadyHasOtherOpenRegister,
    FailedInvalidState,
    FailedStartbelegRequired,
    FailedMonatsbelegRequired,
}

/// <summary>
/// Outcome of a shift close attempt.
/// </summary>
public sealed class CashRegisterCloseResult
{
    public CashRegisterCloseKind Kind { get; init; }

    public static CashRegisterCloseResult Success() =>
        new() { Kind = CashRegisterCloseKind.Success };

    public static CashRegisterCloseResult NotFound() =>
        new() { Kind = CashRegisterCloseKind.FailedNotFound };

    public static CashRegisterCloseResult AlreadyClosed() =>
        new() { Kind = CashRegisterCloseKind.FailedAlreadyClosed };

    public static CashRegisterCloseResult Forbidden() =>
        new() { Kind = CashRegisterCloseKind.FailedForbidden };
}

public enum CashRegisterCloseKind
{
    Success,
    FailedNotFound,
    FailedAlreadyClosed,
    FailedForbidden,
}
