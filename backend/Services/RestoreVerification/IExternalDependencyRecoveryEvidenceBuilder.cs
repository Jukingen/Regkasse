namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// L6 iskeleti: alan bazlı <see cref="ExternalDependencyRecoveryEvidenceBlock"/> (TSE, sırlar, yedekleme araçları, arşiv, FinanzOnline).
/// Canlı donanım/API kanıtı üretmez; rollup <see cref="ExternalDependencyProofState"/> ile üst seviye yanlış yeşil önlenir.
/// </summary>
public interface IExternalDependencyRecoveryEvidenceBuilder
{
    ExternalDependencyRecoveryEvidenceBlock Build();
}
