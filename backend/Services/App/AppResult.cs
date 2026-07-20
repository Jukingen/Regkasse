namespace KasseAPI_Final.Services.App;

/// <summary>Outcome of <see cref="IAppGeneratorService.GenerateAppAsync"/>.</summary>
public sealed class AppResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public string? DownloadUrl { get; private init; }
    public AppType? AppType { get; private init; }

    public static AppResult Success(string downloadUrl, AppType appType) =>
        new()
        {
            Succeeded = true,
            DownloadUrl = downloadUrl,
            AppType = appType
        };

    public static AppResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}
