namespace KasseAPI_Final.Services;

public interface IAdminShiftManagementService
{
    Task<AdminShiftForceCloseResult> ForceCloseRegisterAsync(
        Guid cashRegisterId,
        string actorUserId,
        string actorRole,
        decimal? closingBalance,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed class AdminShiftForceCloseResult
{
    public bool Success { get; init; }
    public AdminShiftForceCloseKind Kind { get; init; }
    public Guid CashRegisterId { get; init; }
    public int ClosedShiftCount { get; init; }

    public static AdminShiftForceCloseResult Succeeded(Guid registerId, int closedShiftCount) =>
        new()
        {
            Success = true,
            Kind = AdminShiftForceCloseKind.Success,
            CashRegisterId = registerId,
            ClosedShiftCount = closedShiftCount,
        };

    public static AdminShiftForceCloseResult NotFound(Guid registerId) =>
        new() { Kind = AdminShiftForceCloseKind.NotFound, CashRegisterId = registerId };

    public static AdminShiftForceCloseResult AlreadyClosed(Guid registerId) =>
        new() { Kind = AdminShiftForceCloseKind.AlreadyClosed, CashRegisterId = registerId };
}

public enum AdminShiftForceCloseKind
{
    Success,
    NotFound,
    AlreadyClosed,
}
