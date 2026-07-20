namespace KasseAPI_Final.Services.App;

public interface IAppGeneratorService
{
    Task<AppResult> GenerateAppAsync(
        Guid tenantId,
        AppType appType,
        CancellationToken ct = default);
}
