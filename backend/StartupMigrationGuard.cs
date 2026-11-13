using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final;

/// <summary>
/// Blocks startup outside Development when EF still has migrations to apply.
/// Development logs a warning and lets <c>Database.Migrate()</c> continue.
/// </summary>
public static class StartupMigrationGuard
{
    public static void Enforce(
        IReadOnlyCollection<string> pendingMigrationIds,
        IHostEnvironment environment,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pendingMigrationIds);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        if (pendingMigrationIds.Count == 0)
            return;

        var ids = pendingMigrationIds as IReadOnlyList<string> ?? pendingMigrationIds.ToArray();
        var joined = string.Join(", ", ids);

        if (environment.IsDevelopment())
        {
            logger.LogWarning(
                "Development startup will apply {Count} pending migration(s): {Migrations}",
                ids.Count,
                joined);
            return;
        }

        logger.LogCritical(
            "Startup blocked: {Count} pending migration(s) outside Development. Pending: {Migrations}",
            ids.Count,
            joined);
        throw new PendingMigrationsException(ids);
    }
}

/// <summary>
/// Raised when startup finds pending EF migrations outside Development.
/// </summary>
public sealed class PendingMigrationsException : InvalidOperationException
{
    public PendingMigrationsException(IReadOnlyList<string> pendingMigrationIds)
        : base($"Pending migrations block startup ({pendingMigrationIds.Count}): {string.Join(", ", pendingMigrationIds)}")
    {
        PendingMigrationIds = pendingMigrationIds;
    }

    public IReadOnlyList<string> PendingMigrationIds { get; }
}
