namespace KasseAPI_Final;

/// <summary>
/// Minimal configuration so <see cref="ApplicationHost.CreateWebApplication"/> can register JWT/EF without user secrets.
/// Not used for real DB access during export (bootstrap is skipped).
/// </summary>
internal static class OpenApiExportConfiguration
{
    internal static IEnumerable<KeyValuePair<string, string?>> BuildInMemoryDefaults() =>
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=127.0.0.1;Port=5432;Database=regkasse_openapi_export;Username=openapi;Password=openapi",
            ["JwtSettings:SecretKey"] = new string('x', 32),
            ["JwtSettings:Issuer"] = "OpenApiExport",
            ["JwtSettings:Audience"] = "OpenApiExport",
        };
}
