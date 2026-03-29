namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Reaper: backup ve restore için ayrı lease süresi ve null-lease grace çarpanı.
/// </summary>
public readonly record struct StaleRunReaperLeaseOptions(
    TimeSpan BackupRunLeaseTimeout,
    double BackupNullLeaseGraceMultiplier,
    TimeSpan RestoreRunLeaseTimeout,
    double RestoreNullLeaseGraceMultiplier);
