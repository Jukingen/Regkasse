using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Optional list filters for <c>GET /api/admin/restore-verification/runs</c>.</summary>
public sealed class RestoreVerificationHistoryFilter
{
    public RestoreVerificationStatus? Status { get; init; }

    public RestoreVerificationTriggerSource? TriggerSource { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    /// <summary>Exact backup-run GUID or case-insensitive prefix (min 8 chars).</summary>
    public string? SourceBackupRunIdSearch { get; init; }
}
