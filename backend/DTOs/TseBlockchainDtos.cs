namespace KasseAPI_Final.DTOs;

public sealed class TseBlockchainSignatureDataDto
{
    public Guid TenantId { get; set; }
    public Guid? SourceId { get; set; }
    public string SourceType { get; set; } = "Signature";
    /// <summary>Compact JWS or signature string to hash (never persisted in full when long).</summary>
    public string SignatureData { get; set; } = string.Empty;
}

public sealed class TseBlockchainRecordDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TransactionHash { get; set; } = string.Empty;
    public string BlockHash { get; set; } = string.Empty;
    public long BlockNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SignatureHash { get; set; } = string.Empty;
    public string? SignaturePreview { get; set; }
    public bool IsVerified { get; set; }
    public bool IsSimulated { get; set; } = true;
    public string NetworkName { get; set; } = "regkasse-sim";
    public Guid? SourceId { get; set; }
    public string SourceType { get; set; } = "Signature";
}

public sealed class TseBlockchainVerificationResultDto
{
    public Guid SignatureId { get; set; }
    public bool IsVerified { get; set; }
    public string Message { get; set; } = string.Empty;
    public TseBlockchainRecordDto? Record { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseBlockchainStatusDto
{
    public string BlockchainStatus { get; set; } = "disconnected";
    public string NetworkName { get; set; } = "regkasse-sim";
    public long CurrentBlock { get; set; }
    public long TotalTransactions { get; set; }
    public bool IsSimulated { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseBlockchainTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionHash { get; set; } = string.Empty;
    public long BlockNumber { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SignatureHash { get; set; } = string.Empty;
    public string? SignaturePreview { get; set; }
}
