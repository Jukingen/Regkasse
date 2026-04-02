using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Yapılandırma + kalıcı admin modundan etkin <see cref="BackupExecutionAdapterKind"/> türetir.
/// </summary>
public static class BackupEffectiveExecutionAdapterResolver
{
    public static BackupExecutionAdapterKind ResolveEffectiveAdapterKind(
        BackupOptions options,
        AdminBackupRuntimeExecutionMode adminMode) =>
        adminMode switch
        {
            AdminBackupRuntimeExecutionMode.InheritFromConfiguration => options.ExecutionAdapterKind,
            AdminBackupRuntimeExecutionMode.SimulatedFake => BackupExecutionAdapterKind.Fake,
            AdminBackupRuntimeExecutionMode.PostgreSqlPgDump => BackupExecutionAdapterKind.PgDump,
            _ => options.ExecutionAdapterKind
        };
}
