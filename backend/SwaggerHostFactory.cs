namespace KasseAPI_Final;

/// <summary>
/// Swashbuckle.AspNetCore.Cli discovers this type and calls <see cref="CreateHost"/> instead of legacy Startup.
/// See: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/src/Swashbuckle.AspNetCore.Cli/Program.cs
/// </summary>
public static class SwaggerHostFactory
{
    public static IHost CreateHost()
    {
        OpenApiExportMode.ToolingExportActive = true;
        Environment.SetEnvironmentVariable(OpenApiExportMode.EnvironmentVariableName, "true");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        return ApplicationHost.CreateWebApplication(Array.Empty<string>());
    }
}
