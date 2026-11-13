namespace KasseAPI_Final.DTOs;

public sealed class RksvAusfallEpisodeDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceSerial { get; set; }
    public string EpisodeType { get; set; } = string.Empty;
    public string OperationKind { get; set; } = string.Empty;
    public string Begruendung { get; set; } = string.Empty;
    public DateTimeOffset? BeginnUtc { get; set; }
    public DateTimeOffset? EndeUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? OutboxMessageId { get; set; }
    public string? ExternalReference { get; set; }
    public string? CertificateSerial { get; set; }
    public string? KassenId { get; set; }
    public Guid? CashRegisterId { get; set; }
    public Guid? RelatedAusfallEpisodeId { get; set; }
    public string? OperatorNote { get; set; }
    public string? CreatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

public sealed class RksvAusfallTriggerRequest
{
    public Guid? DeviceId { get; set; }
    public Guid? CashRegisterId { get; set; }

    /// <summary>SCU or Kasse.</summary>
    public string EpisodeType { get; set; } = "SCU";

    /// <summary>Ausfall or Wiederinbetriebnahme.</summary>
    public string OperationKind { get; set; } = "Ausfall";

    public string Begruendung { get; set; } = "SONSTIGES";
    public DateTimeOffset? BeginnUtc { get; set; }
    public DateTimeOffset? EndeUtc { get; set; }
    public string? CertificateSerial { get; set; }
    public string? KassenId { get; set; }
    public Guid? RelatedAusfallEpisodeId { get; set; }
    public string? OperatorNote { get; set; }

    /// <summary>When true, enqueue immediately (requires finanzonline.submit). Otherwise Suggested/PendingApproval.</summary>
    public bool EnqueueImmediately { get; set; }
}

public sealed class RksvAusfallTriggerResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public RksvAusfallEpisodeDto? Episode { get; set; }
}

public sealed class RksvAusfallApproveRequest
{
    public string? OperatorNote { get; set; }
}

public sealed class RksvAusfallMarkManualRequest
{
    public string? OperatorNote { get; set; }
    public string? ExternalReference { get; set; }
}
