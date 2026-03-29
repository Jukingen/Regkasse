namespace KasseAPI_Final.Models;

/// <summary>backup_runs ve restore_verification_runs için ortak lease/heartbeat kolonları.</summary>
public interface IRunLeaseColumns
{
    DateTime? LastHeartbeatAtUtc { get; set; }

    DateTime? LeaseExpiresAtUtc { get; set; }
}
