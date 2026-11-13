using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IPosDailyClosingService
{
    Task<PosDailyClosingStatusDto> GetStatusAsync(string cashierUserId, CancellationToken cancellationToken = default);

    Task<PosDailyClosingResult> PerformDailyClosingAsync(
        string cashierUserId,
        string actorRole,
        PosDailyClosingRequest request,
        CancellationToken cancellationToken = default);
}

public enum PosDailyClosingFailureKind
{
    NoActiveShift,
    AlreadyClosed,
    FiscalBlocked,
    FiscalFailed,
    RegisterCloseForbidden,
    RegisterCloseFailed,
}

public sealed class PosDailyClosingException : Exception
{
    public PosDailyClosingFailureKind Kind { get; }
    public int PaymentsWithoutInvoiceCount { get; }

    public PosDailyClosingException(
        PosDailyClosingFailureKind kind,
        string message,
        int paymentsWithoutInvoiceCount = 0) : base(message)
    {
        Kind = kind;
        PaymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount;
    }
}
