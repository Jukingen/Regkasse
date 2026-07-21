namespace KasseAPI_Final.Services.Rksv;

/// <summary>
/// Runs BMF <c>regkassen-verification-depformat</c> Prüftool when JDK 17+ and JARs are available.
/// </summary>
public interface IRksvDepPrueftoolRunner
{
    bool IsAvailable(out string? unavailableReason);

    RksvDepPrueftoolRunResult RunCheckDepExport(
        string depExportPath,
        string cryptoMaterialPath,
        string outputDirectory);
}

public sealed record RksvDepPrueftoolRunResult(
    int ExitCode,
    string? VerificationState,
    string StdOut,
    string StdErr);
