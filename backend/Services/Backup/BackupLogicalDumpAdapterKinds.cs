using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Adapter kinds that produce a real PostgreSQL custom-format logical dump
/// eligible for restore drills when <c>AllowNonPgDumpBackupSource</c> is false.
/// </summary>
public static class BackupLogicalDumpAdapterKinds
{
    public const string PgDump = nameof(BackupExecutionAdapterKind.PgDump);

    /// <summary>System strategy composite: <c>pg_dump -Fc</c> plus structured system ZIP.</summary>
    public const string SystemComposite = CompositeSystemBackupExecutionAdapter.AdapterKindValue;

    /// <summary>EF-translatable set for <c>AdapterKind</c> filters.</summary>
    public static readonly string[] SqlComparableKinds = [PgDump, SystemComposite];

    public static bool IsPgDumpEquivalent(string? adapterKind) =>
        string.Equals(adapterKind, PgDump, StringComparison.OrdinalIgnoreCase)
        || string.Equals(adapterKind, SystemComposite, StringComparison.OrdinalIgnoreCase);
}
