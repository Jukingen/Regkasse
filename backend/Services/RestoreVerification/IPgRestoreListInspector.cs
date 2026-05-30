namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Read-only inspection of a custom-format pg_dump file (<c>pg_restore --list</c>); no database restore.
/// </summary>
public interface IPgRestoreListInspector
{
    Task<PgRestoreListInspectResult> InspectDumpFileAsync(string absoluteDumpPath, CancellationToken cancellationToken = default);

    /// <summary>Runs <c>pg_restore --list</c> and parses TABLE DATA TOC entries.</summary>
    Task<PgRestoreListTableCatalogResult> ReadTableDataCatalogAsync(
        string absoluteDumpPath,
        CancellationToken cancellationToken = default);
}
