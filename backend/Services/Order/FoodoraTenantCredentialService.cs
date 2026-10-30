using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IFoodoraTenantCredentialService
{
    Task<(OnlineOrderIntegrationCommandResult Result, FoodoraTenantCredentialStatusDto? Body)> GetAsync(
        CancellationToken ct = default);

    Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertFoodoraTenantCredentialRequest request,
        CancellationToken ct = default);
}

public sealed class FoodoraTenantCredentialService : IFoodoraTenantCredentialService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly TimeProvider _time;

    public FoodoraTenantCredentialService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        OnlineOrderIntegrationTokenProtector protector,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _protector = protector;
        _time = time;
    }

    public async Task<(OnlineOrderIntegrationCommandResult Result, FoodoraTenantCredentialStatusDto? Body)> GetAsync(
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return (Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found."), null);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var provider = await db.OnlineOrderIntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider && p.IsActive,
                ct);
        var row = await db.OnlineOrderIntegrationTenantCredentials.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                     && c.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider
                     && c.IsActive,
                ct);
        return (OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK), new FoodoraTenantCredentialStatusDto
        {
            ProviderEnabled = provider?.Enabled == true,
            CredentialStatus = IsConfigured(row)
                ? OnlineOrderIntegrationProviderStatuses.Configured
                : OnlineOrderIntegrationProviderStatuses.Missing,
        });
    }

    public async Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertFoodoraTenantCredentialRequest request,
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        var clientId = request.ClientId?.Trim() ?? string.Empty;
        var clientSecret = request.ClientSecret?.Trim() ?? string.Empty;
        var chainId = request.ChainId?.Trim() ?? string.Empty;
        var webhookToken = request.WebhookToken?.Trim() ?? string.Empty;
        if (clientId.Length is < 1 or > 256
            || clientSecret.Length is < 1 or > 512
            || chainId.Length is < 1 or > 64
            || webhookToken.Length is < 1 or > 512)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Foodora credentials are incomplete.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var provider = await db.OnlineOrderIntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider && p.IsActive,
                ct);
        if (provider is null || !provider.Enabled)
            return Fail(StatusCodes.Status409Conflict, OnlineOrderIntegrationProviderStatuses.DisabledCode, "Provider is disabled.");

        var now = _time.GetUtcNow().UtcDateTime;
        var row = await db.OnlineOrderIntegrationTenantCredentials
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                     && c.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider,
                ct);
        if (row is null)
        {
            row = new OnlineOrderIntegrationTenantCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = OnlineOrderIntegrationSignatureStatus.FoodoraProvider,
                CreatedAt = now,
                IsActive = true,
            };
            db.OnlineOrderIntegrationTenantCredentials.Add(row);
        }

        row.ClientId = _protector.Protect(clientId);
        row.ClientSecret = _protector.Protect(clientSecret);
        row.ChainId = chainId;
        row.WebhookToken = _protector.Protect(webhookToken);
        row.AccessToken = null;
        row.AccessTokenExpiresAt = null;
        row.UpdatedAt = now;
        row.IsActive = true;
        await db.SaveChangesAsync(ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    private static bool IsConfigured(OnlineOrderIntegrationTenantCredential? row) =>
        row is not null
        && !string.IsNullOrWhiteSpace(row.ClientId)
        && !string.IsNullOrWhiteSpace(row.ClientSecret)
        && !string.IsNullOrWhiteSpace(row.ChainId)
        && !string.IsNullOrWhiteSpace(row.WebhookToken);

    private static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        OnlineOrderIntegrationCommandResult.Fail(statusCode, code, error);
}
