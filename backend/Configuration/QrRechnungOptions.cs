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

    /// <summary>
    /// Reserved bank-submission switch. Default <c>false</c>. No HTTP client reads this.
    /// Production and Staging reject <c>true</c> at startup.
    /// </summary>
    public QrRechnungBankSubmitOptions BankSubmit { get; set; } = new();
}

/// <summary><c>QrRechnung:BankSubmit</c>. Kept off until a later bank package.</summary>
public sealed class QrRechnungBankSubmitOptions
{
    public const string SectionName = "BankSubmit";

    public bool Enabled { get; set; }
}
