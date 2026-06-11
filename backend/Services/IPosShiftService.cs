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
