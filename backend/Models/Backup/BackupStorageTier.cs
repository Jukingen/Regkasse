namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Cost-oriented storage class for a backup artifact (not fiscal lifecycle).
/// Hot = staging / fast access; Warm = aged staging; Cold = prefer external archive.
/// </summary>
public enum BackupStorageTier
{
    Hot = 0,
    Warm = 1,
    Cold = 2
}
