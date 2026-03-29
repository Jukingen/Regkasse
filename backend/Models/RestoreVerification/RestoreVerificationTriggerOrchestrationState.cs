namespace KasseAPI_Final.Models.RestoreVerification;

/// <summary>Manuel restore drill tetikleme sonucu (API yanıtı ile uyumlu).</summary>
public enum RestoreVerificationTriggerOrchestrationState
{
    NewlyQueued = 0,
    ExistingByIdempotencyKey = 1,
    ExistingActiveRunReturned = 2
}
