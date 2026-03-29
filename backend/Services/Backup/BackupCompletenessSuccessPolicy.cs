using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Terminal başarı ile artifact completeness (LogicalDump varlığı) arasındaki ilişki; tek kaynak.
/// </summary>
public static class BackupCompletenessSuccessPolicy
{
    /// <summary>
    /// Run satırındaki <c>adapter_kind</c> dizesini enum’a çevirir.
    /// </summary>
    public static bool TryParseAdapterKind(string? adapterKind, out BackupExecutionAdapterKind kind)
    {
        kind = default;
        if (string.IsNullOrWhiteSpace(adapterKind))
            return false;
        return Enum.TryParse(adapterKind.Trim(), ignoreCase: true, out kind);
    }

    /// <summary>
    /// Bu adapter için terminal <see cref="BackupRunStatus.Succeeded"/>, doğrulama geçtikten sonra
    /// <see cref="BackupVerificationOutcome.CompletenessFlag"/> true olmadan verilmez.
    /// </summary>
    public static bool CompletenessRequiredForSucceededRun(BackupExecutionAdapterKind adapterKind) =>
        adapterKind == BackupExecutionAdapterKind.PgDump;

    /// <summary>
    /// API yanıtında operatörün completeness bayrağını nasıl yorumlayacağı (İngilizce).
    /// </summary>
    public static string FormatCompletenessPolicyNote(string? adapterKindString)
    {
        if (TryParseAdapterKind(adapterKindString, out var k) && CompletenessRequiredForSucceededRun(k))
        {
            return "For PgDump, terminal Succeeded requires staging verification to pass and the verified artifact set to include a logical dump (CompletenessFlag=true). " +
                   "This is artifact/metadata verification only — not restore proof.";
        }

        return "For this adapter, CompletenessFlag is informational: it indicates whether a LogicalDump artifact was present in the verified set. " +
               "Terminal Succeeded means verification passed and the pipeline steps completed; it does not by itself imply a real PostgreSQL logical backup unless the execution adapter is PgDump.";
    }

    /// <summary>
    /// Doğrulama Passed sonrası completeness eksikse terminal başarıya izin verilmez; sebep döner.
    /// </summary>
    public static string? GetIncompleteVerifiedArtifactSetFailureReason(
        BackupExecutionAdapterKind adapterKind,
        BackupVerificationOutcome outcome)
    {
        if (!outcome.Passed || !CompletenessRequiredForSucceededRun(adapterKind) || outcome.CompletenessFlag)
            return null;

        return "Verified artifact set does not include a logical dump (LogicalDump); ExecutionAdapterKind=PgDump requires it for terminal Succeeded.";
    }
}
