using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IPosShiftService
{
    Task<CurrentShiftResponse> GetCurrentShiftAsync(string cashierUserId, CancellationToken cancellationToken = default);

    Task<CashierShiftDto> StartShiftAsync(
        string cashierUserId,
        string cashierDisplayName,
        StartShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<EndShiftResponse> EndShiftAsync(
        string cashierUserId,
        string actorRole,
        EndShiftRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotent auto-open. Resolves the register from <paramref name="cashRegisterId"/> or
    /// <see cref="Models.UserSettings.CashRegisterId"/>. Returns a structured result instead of throwing
    /// for missing/unavailable registers so POS can guide the cashier.
    /// </summary>
    Task<ShiftAutoOpenResult> AutoOpenShiftAsync(
        string cashierUserId,
        string cashierDisplayName,
        Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-closes the caller's active CashierShift without closing the cash register.
    /// No-op when there is no active shift (idempotent for logout).
    /// </summary>
    Task<CashierShiftDto?> AutoCloseShiftAsync(
        string cashierUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<ShiftTotalsDto> GetShiftTotalsAsync(
        Guid cashRegisterId,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        CancellationToken cancellationToken = default);
}

public enum PosShiftStartResultKind
{
    Success,
    AlreadyActive,
    RegisterNotFound,
    RegisterNotAssigned,
    RegisterOpenConflict,
    RegisterOpenFailed,
}

public sealed class PosShiftStartException : Exception
{
    public PosShiftStartResultKind Kind { get; }

    public PosShiftStartException(PosShiftStartResultKind kind, string message) : base(message)
    {
        Kind = kind;
    }
}

public enum PosShiftEndResultKind
{
    Success,
    NoActiveShift,
    RegisterCloseForbidden,
    RegisterCloseFailed,
}

public sealed class PosShiftEndException : Exception
{
    public PosShiftEndResultKind Kind { get; }

    public PosShiftEndException(PosShiftEndResultKind kind, string message) : base(message)
    {
        Kind = kind;
    }
}
