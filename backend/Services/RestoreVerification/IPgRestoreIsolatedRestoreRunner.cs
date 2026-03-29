namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Yönetim bağlantısı (CREATEDB) ile geçici DB oluşturup <c>pg_restore</c> çalıştırır; bitince DB’yi düşürür.
/// </summary>
public interface IPgRestoreIsolatedRestoreRunner
{
    /// <param name="adminConnectionString">Genelde <c>postgres</c> DB’sine bağlanan; CREATEDB yetkisi.</param>
    Task<PgRestoreIsolatedRestoreOutcome> RestoreCustomDumpToEphemeralDatabaseAsync(
        string adminConnectionString,
        string absoluteCustomDumpPath,
        string newDatabaseName,
        string? pgRestoreExecutablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
