namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// İzole geri yüklenen PostgreSQL veritabanına karşı salt okunur uygulama dumanı (EF şema + kritik tablolar).
/// </summary>
public interface IRestoredDatabaseApplicationSmokeRunner
{
    Task<RestoredDatabaseApplicationSmokeOutcome> RunAsync(string restoredDatabaseConnectionString, CancellationToken ct);
}
