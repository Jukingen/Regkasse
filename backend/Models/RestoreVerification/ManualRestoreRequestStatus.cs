namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>Manual restore approval workflow lifecycle (validation-only isolated restore).</summary>
public enum ManualRestoreRequestStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2,
    Executing = 3,
    Completed = 4,
    Failed = 5,
}
