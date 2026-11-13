namespace KasseAPI_Final.Tests;

/// <summary>
/// Clears process-wide <see cref="OpenApiExportMode"/> flags. Call from unit tests that require
/// license enforcement; those classes must also sit in <c>OpenApiExportWebHost</c> so they do not
/// overlap WebApplicationFactory hosts that set REGKASSE_OPENAPI_EXPORT=true.
/// </summary>
internal static class OpenApiExportHostGate
{
    private static readonly object Sync = new();

    /// <summary>Call from unit tests that require OpenAPI export mode to be off.</summary>
    public static void EnsureExportModeDisabled()
    {
        lock (Sync)
        {
            OpenApiExportMode.ToolingExportActive = false;
            Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, null);
            Environment.SetEnvironmentVariable(OpenApiExportMode.IntegrationTestInMemoryDatabaseEnvironmentVariable, null);
        }
    }
}
