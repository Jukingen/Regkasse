namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Başarılı yedek çalıştırmalarından türetilen süre istatistiği (admin UI tahmini).
/// </summary>
public sealed class BackupSucceededDurationStatistics
{
    /// <summary>Ortalama süre saniye; örnek yoksa null.</summary>
    public double? AverageDurationSeconds { get; init; }

    /// <summary>Kullanılan örnek sayısı.</summary>
    public int SampleCount { get; init; }
}
