using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderIntegrationProviderService
{
    Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertOnlineOrderIntegrationProviderRequest request,
        CancellationToken ct = default);

    Task<(OnlineOrderIntegrationCommandResult Result, OnlineOrderIntegrationProviderAdminDto? Body)> GetAdminAsync(
        string? provider,
        CancellationToken ct = default);

    Task<(OnlineOrderIntegrationCommandResult Result, OnlineOrderIntegrationProviderTenantDto? Body)> GetTenantAsync(
        string? provider,
        CancellationToken ct = default);

    /// <summary>
    /// Partner credentials for an enabled provider. Missing or disabled is HTTP 409.
    /// </summary>
    Task<(OnlineOrderIntegrationCommandResult Result, OnlineOrderIntegrationProviderCredentials? Credentials)> RequireEnabledCredentialsAsync(
        string provider,
        CancellationToken ct = default);

    /// <summary>Webhook HMAC secret when the provider row is enabled. Otherwise null.</summary>
    Task<string?> GetEnabledClientSecretAsync(string provider, CancellationToken ct = default);
}

public sealed class OnlineOrderIntegrationProviderService : IOnlineOrderIntegrationProviderService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly OnlineOrderIntegrationTokenProtector _protector;
    private readonly TimeProvider _time;

    public OnlineOrderIntegrationProviderService(
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

    public async Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertOnlineOrderIntegrationProviderRequest request,
        CancellationToken ct = default)
    {
        var provider = Normalize(request.Provider);
        if (provider is null)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrationProviders
            .FirstOrDefaultAsync(p => p.Provider == provider, ct);
        var clientId = request.ClientId?.Trim();
        var clientSecret = request.ClientSecret?.Trim();
        var hasSecrets = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
        if (row is null && !hasSecrets)
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Client id and client secret are required.");
        if ((string.IsNullOrWhiteSpace(clientId) ^ string.IsNullOrWhiteSpace(clientSecret)))
            return Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Client id and client secret must be sent together.");

        var now = _time.GetUtcNow().UtcDateTime;
        if (row is null)
        {
            row = new OnlineOrderIntegrationProvider
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                CreatedAt = now,
                IsActive = true,
            };
            db.OnlineOrderIntegrationProviders.Add(row);
        }

        if (hasSecrets)
        {
            row.ClientId = _protector.Protect(clientId!);
            row.ClientSecret = _protector.Protect(clientSecret!);
        }

        row.Enabled = request.Enabled;
        row.UpdatedAt = now;
        row.IsActive = true;
        await db.SaveChangesAsync(ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    public async Task<(OnlineOrderIntegrationCommandResult Result, OnlineOrderIntegrationProviderAdminDto? Body)> GetAdminAsync(
        string? provider,
        CancellationToken ct = default)
    {
        var normalized = Normalize(provider);
        if (normalized is null)
            return (Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid."), null);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Provider == normalized && p.IsActive, ct);
        return (OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK), ToAdmin(normalized, row));
    }

    public async Task<(OnlineOrderIntegrationCommandResult Result, OnlineOrderIntegrationProviderTenantDto? Body)> GetTenantAsync(
        string? provider,
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return (Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found."), null);

        var normalized = Normalize(provider);
        if (normalized is null)
            return (Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid."), null);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Provider == normalized && p.IsActive, ct);
        var connected = await db.OnlineOrderIntegrationOAuthTokens.AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId && t.Provider == normalized && t.IsActive, ct);
        var admin = ToAdmin(normalized, row);
        return (OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK), new OnlineOrderIntegrationProviderTenantDto
        {
            Provider = admin.Provider,
            Enabled = admin.Enabled,
            CredentialStatus = admin.CredentialStatus,
            Connected = connected,
        });
    }

    public async Task<(OnlineOrderIntegrationCommandResult Result, OnlineOrderIntegrationProviderCredentials? Credentials)> RequireEnabledCredentialsAsync(
        string provider,
        CancellationToken ct = default)
    {
        var normalized = Normalize(provider);
        if (normalized is null)
            return (Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid."), null);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrationProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Provider == normalized && p.IsActive, ct);
        if (row is null || !row.Enabled || !HasSecrets(row))
            return (Disabled(), null);

        try
        {
            return (OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK), new OnlineOrderIntegrationProviderCredentials
            {
                ClientId = _protector.Unprotect(row.ClientId),
                ClientSecret = _protector.Unprotect(row.ClientSecret),
            });
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
        {
            return (Disabled(), null);
        }
    }

    public async Task<string?> GetEnabledClientSecretAsync(string provider, CancellationToken ct = default)
    {
        var required = await RequireEnabledCredentialsAsync(provider, ct);
        return required.Credentials?.ClientSecret;
    }

    private static OnlineOrderIntegrationProviderAdminDto ToAdmin(string provider, OnlineOrderIntegrationProvider? row) =>
        new()
        {
            Provider = provider,
            Enabled = row?.Enabled == true,
            CredentialStatus = row is not null && HasSecrets(row)
                ? OnlineOrderIntegrationProviderStatuses.Configured
                : OnlineOrderIntegrationProviderStatuses.Missing,
        };

    private static bool HasSecrets(OnlineOrderIntegrationProvider row) =>
        !string.IsNullOrWhiteSpace(row.ClientId) && !string.IsNullOrWhiteSpace(row.ClientSecret);

    private static string? Normalize(string? provider)
    {
        var value = provider?.Trim().ToLowerInvariant() ?? string.Empty;
        return value.Length is < 1 or > 32 ? null : value;
    }

    private static OnlineOrderIntegrationCommandResult Disabled() =>
        Fail(StatusCodes.Status409Conflict, OnlineOrderIntegrationProviderStatuses.DisabledCode, "Provider is disabled.");

    private static OnlineOrderIntegrationCommandResult Fail(int statusCode, string code, string error) =>
        OnlineOrderIntegrationCommandResult.Fail(statusCode, code, error);
}
