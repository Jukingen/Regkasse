namespace KasseAPI_Final.Configuration;

/// <summary>
/// Stub for planned CH QR-bill payload options. No bank submission.
/// Startup lock lives in <c>CountryFiscalLockEvaluator</c>.
/// </summary>
public sealed class QrRechnungOptions
{
    public const string SectionName = "QrRechnung";

    /// <summary>Production stub uses <c>not-configured</c>; <c>dryRun</c> is rejected outside Development.</summary>
    public string BuilderMode { get; set; } = "not-configured";
}
