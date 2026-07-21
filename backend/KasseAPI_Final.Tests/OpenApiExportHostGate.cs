namespace KasseAPI_Final.Tests;

/// <summary>
/// Process-wide gate for hosts that mutate <see cref="OpenApiExportMode"/> environment variables.
/// Prevents overlapping WebApplicationFactory lifetimes from leaking REGKASSE_OPENAPI_EXPORT=true
/// into unit tests (e.g. LicenseMiddleware).
/// </summary>
internal static class OpenApiExportHostGate
{
    private static readonly object Sync = new();
    private static int _holders;

    public static void Enter()
    {
        Monitor.Enter(Sync);
        _holders++;
    }

    public static void Exit()
    {
        _holders = Math.Max(0, _holders - 1);
        if (_holders == 0)
        {
            Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, null);
            Environment.SetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable, null);
        }

        Monitor.Exit(Sync);
    }

    /// <summary>Call from unit tests that require OpenAPI export mode to be off.</summary>
    public static void EnsureExportModeDisabled()
    {
        lock (Sync)
        {
            if (_holders > 0)
                return;

            Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, null);
            Environment.SetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable, null);
        }
    }
}
