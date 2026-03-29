namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore verification worker dağıtık kapı denemesi; backup worker kapısından bağımsız advisory anahtar çifti.
/// </summary>
public enum RestoreVerificationOrchestratorGateAttempt
{
    /// <summary>RestoreVerification:OrchestratorDistributedLockEnabled=false — çoklu örnek riski bilinçli.</summary>
    DisabledBypass = 0,

    AcquiredLease = 1,

    /// <summary>Başka oturum aynı (k1,k2) kilidini tutuyor; bu tick atlanır.</summary>
    ContendedElsewhere = 2,

    ConnectionFailed = 3
}
