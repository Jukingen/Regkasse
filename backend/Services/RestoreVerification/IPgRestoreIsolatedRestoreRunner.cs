namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Shell yok; <c>pg_restore</c> child process + Npgsql ile CREATE DATABASE; isteğe bağlı olarak DROP ertelenir (geri yükleme sonrası SQL için).
/// </summary>
public interface IPgRestoreIsolatedRestoreRunner
{
    /// <param name="dropEphemeralDatabaseAfterRestore">False: başarılı geri yüklemeden sonra DB korunur (sonrasında <see cref="DropEphemeralDatabaseAsync"/> ile silinmeli).</param>
    Task<PgRestoreIsolatedRestoreOutcome> RestoreCustomDumpToEphemeralDatabaseAsync(
        string adminConnectionString,
        string absoluteCustomDumpPath,
        string newDatabaseName,
        string? pgRestoreExecutablePath,
        TimeSpan timeout,
        bool dropEphemeralDatabaseAfterRestore = true,
        CancellationToken cancellationToken = default);

    /// <summary>Yönetim bağlantısı ile geçici veritabanını düşürür (host/kimlik bilgisi loglanmaz).</summary>
    Task DropEphemeralDatabaseAsync(
        string adminConnectionString,
        string databaseName,
        CancellationToken cancellationToken = default);
}
