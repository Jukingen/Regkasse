namespace KasseAPI_Final.DTOs;

/// <summary>Realtime payload for Fiskaly operation status (SignalR <c>OperationStatus</c>).</summary>
public sealed class FiskalyOperationStatusEventDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public string OperationType { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int ProgressPercent { get; init; }

    public Guid CashRegisterId { get; init; }

    public string? CashRegisterName { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }
}
