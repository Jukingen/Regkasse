namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Development-only probe of <c>pg_dump --version</c> / <c>pg_restore --version</c> (process spawn; not used for production semantics).
/// </summary>
public sealed class BackupPostgresClientToolingSnapshot
{
    public static BackupPostgresClientToolingSnapshot SkippedNotApplicable { get; } = new()
    {
        ProbesSkipped = true,
        SkipReason = "not_development_or_not_pg_dump_adapter"
    };

    public bool ProbesSkipped { get; init; }

    /// <summary>Machine reason when <see cref="ProbesSkipped"/> is true.</summary>
    public string SkipReason { get; init; } = string.Empty;

    public string PgDumpExecutableToken { get; init; } = string.Empty;

    public bool PgDumpProbeSucceeded { get; init; }

    /// <summary>E.g. START_FAILED, TIMEOUT, NONZERO_EXIT, EXCEPTION, MISSING_OUTPUT.</summary>
    public string? PgDumpFailureKind { get; init; }

    public string? PgDumpVersionLine { get; init; }

    public string PgRestoreExecutableToken { get; init; } = string.Empty;

    public bool PgRestoreProbeSucceeded { get; init; }

    public string? PgRestoreFailureKind { get; init; }

    public string? PgRestoreVersionLine { get; init; }

    /// <summary>İngilizce tanı listesi — API ve loglar için.</summary>
    public IReadOnlyList<BackupConfigurationDiagnostic> ToDiagnostics()
    {
        if (ProbesSkipped)
            return Array.Empty<BackupConfigurationDiagnostic>();

        var list = new List<BackupConfigurationDiagnostic>(4);

        if (PgDumpProbeSucceeded)
        {
            list.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.DevPgDumpClientOk,
                Severity = BackupConfigurationDiagnosticSeverity.Information,
                Message =
                    $"PostgreSQL client tooling: pg_dump is callable ({PgDumpExecutableToken}).{(PgDumpVersionLine != null ? $" {PgDumpVersionLine}" : string.Empty)}",
                RelatedConfigurationKeys = new[] { "Backup:PgDumpExecutablePath" }
            });
        }
        else
        {
            list.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.DevPgDumpClientMissingOrBroken,
                Severity = BackupConfigurationDiagnosticSeverity.Warning,
                Message =
                    $"PostgreSQL client tooling: pg_dump is not usable from this process ({PgDumpExecutableToken}); failureKind={PgDumpFailureKind ?? "unknown"} — install PostgreSQL client tools or set Backup:PgDumpExecutablePath.",
                RelatedConfigurationKeys = new[] { "Backup:PgDumpExecutablePath" }
            });
        }

        if (PgRestoreProbeSucceeded)
        {
            list.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.DevPgRestoreClientOk,
                Severity = BackupConfigurationDiagnosticSeverity.Information,
                Message =
                    $"PostgreSQL client tooling: pg_restore is callable ({PgRestoreExecutableToken}).{(PgRestoreVersionLine != null ? $" {PgRestoreVersionLine}" : string.Empty)}",
                RelatedConfigurationKeys = new[] { "RestoreVerification:PgRestoreExecutablePath" }
            });
        }
        else
        {
            list.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.DevPgRestoreClientMissingOrBroken,
                Severity = BackupConfigurationDiagnosticSeverity.Warning,
                Message =
                    $"PostgreSQL client tooling: pg_restore is not usable from this process ({PgRestoreExecutableToken}); failureKind={PgRestoreFailureKind ?? "unknown"} — install PostgreSQL client tools or set RestoreVerification:PgRestoreExecutablePath (restore drills / verification).",
                RelatedConfigurationKeys = new[] { "RestoreVerification:PgRestoreExecutablePath" }
            });
        }

        return list;
    }
}
