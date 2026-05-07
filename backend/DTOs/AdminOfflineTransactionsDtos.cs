using System;
using System.Collections.Generic;

namespace KasseAPI_Final.DTOs;

/// <summary>Summary counters for admin offline queue dashboard widgets.</summary>
public sealed class AdminOfflineTransactionsSummaryDto
{
    public int PendingCount { get; set; }

    public int FailedCount { get; set; }

    /// <summary>Latest UTC timestamp of any replay attempt (manual or worker), or null if none.</summary>
    public DateTime? LastReplayAtUtc { get; set; }
}

public sealed class AdminOfflineTransactionsListResponse
{
    public IReadOnlyList<AdminOfflineTransactionRowDto> Items { get; set; } = Array.Empty<AdminOfflineTransactionRowDto>();

    public int TotalCount { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }
}

public sealed class AdminOfflineTransactionRowDto
{
    public Guid Id { get; set; }

    public Guid CashRegisterId { get; set; }

    /// <summary>Human-readable register label (number + location).</summary>
    public string CashRegisterLabel { get; set; } = string.Empty;

    public DateTime ServerReceivedAtUtc { get; set; }

    /// <summary>Gross total from stored payload (hint).</summary>
    public decimal Amount { get; set; }

    /// <summary>Normalized payment method from payload: cash, card, voucher, or unknown.</summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Raw <see cref="Models.OfflineTransactionStatus"/> name.</summary>
    public string Status { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public string? LastErrorCode { get; set; }

    public string? LastErrorMessageSafe { get; set; }

    public Guid? SyncedPaymentId { get; set; }
}

public sealed class AdminOfflineTransactionRetryResponseDto
{
    public Guid? ReplayBatchCorrelationId { get; set; }

    public IReadOnlyList<ReplayOfflineTransactionsResponseItem> Items { get; set; } =
        Array.Empty<ReplayOfflineTransactionsResponseItem>();

    public int QueuedCount { get; set; }
}
