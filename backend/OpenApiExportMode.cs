namespace KasseAPI_Final;

/// <summary>
/// When true, the host is built for Swashbuckle CLI / OpenAPI export only: no DB bootstrap, tooling config defaults apply.
/// Set automatically by <see cref="SwaggerHostFactory"/>; do not use in production serving paths.
/// </summary>
public static class OpenApiExportMode
{
    /// <summary>When set, <see cref="ApplicationHost"/> uses EF InMemory with this database name (integration tests only).</summary>
    public const string IntegrationTestInMemoryDatabaseEnvironmentVariable = "REGKASSE_TEST_INMEMORY_DB";

    public const string EnvironmentVariableName = "REGKASSE_OPENAPI_EXPORT";

    /// <summary>
    /// Set by <see cref="SwaggerHostFactory"/> for the Swashbuckle CLI child process. Environment variables are not always
    /// observable the same way across hosts; this stays true for the lifetime of that process (normal <c>dotnet run</c> never sets it).
    /// </summary>
    internal static bool ToolingExportActive { get; set; }

    public static bool IsEnabled =>
        ToolingExportActive
        || string.Equals(Environment.GetEnvironmentVariable(EnvironmentVariableName), "true", StringComparison.OrdinalIgnoreCase);
}
