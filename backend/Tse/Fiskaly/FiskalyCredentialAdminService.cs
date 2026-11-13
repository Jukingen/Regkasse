using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tse.Fiskaly;

public interface IFiskalyCredentialAdminService
{
    Task<FiskalyTenantCredentialStatusDto?> GetAsync(CancellationToken cancellationToken = default);

    Task<FiskalyCredentialCommandResult> UpsertAsync(
        UpsertFiskalyTenantCredentialRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed record FiskalyCredentialCommandResult(
    int StatusCode,
    FiskalyTenantCredentialStatusDto? Body,
    string? Error)
{
    public static FiskalyCredentialCommandResult Ok(FiskalyTenantCredentialStatusDto body) =>
        new(StatusCodes.Status200OK, body, null);

    public static FiskalyCredentialCommandResult Fail(int statusCode, string error) =>
        new(statusCode, null, error);
}

public sealed class FiskalyCredentialAdminService : IFiskalyCredentialAdminService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly FiskalyCredentialProtector _protector;
    private readonly IFiskalyTenantCredentialResolver _resolver;
    private readonly FiskalyAccessTokenCache _tokenCache;
    private readonly IAuditLogService _auditLog;

    public FiskalyCredentialAdminService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        FiskalyCredentialProtector protector,
        IFiskalyTenantCredentialResolver resolver,
        FiskalyAccessTokenCache tokenCache,
        IAuditLogService auditLog)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _protector = protector;
        _resolver = resolver;
        _tokenCache = tokenCache;
        _auditLog = auditLog;
    }

    public async Task<FiskalyTenantCredentialStatusDto?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantFiskalyCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        return Map(row);
    }

    public async Task<FiskalyCredentialCommandResult> UpsertAsync(
        UpsertFiskalyTenantCredentialRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return FiskalyCredentialCommandResult.Fail(StatusCodes.Status404NotFound, "Tenant was not found.");

        var apiKey = request.ApiKey?.Trim() ?? string.Empty;
        var apiSecret = request.ApiSecret?.Trim() ?? string.Empty;
        var scuId = request.ScuId?.Trim() ?? string.Empty;
        var environment = request.Environment?.Trim().ToUpperInvariant() ?? string.Empty;

        if (!Guid.TryParse(scuId, out var scuGuid) || scuGuid == Guid.Empty)
            return FiskalyCredentialCommandResult.Fail(StatusCodes.Status400BadRequest, "SCU id must be a UUID.");

        if (environment is not ("TEST" or "LIVE"))
            return FiskalyCredentialCommandResult.Fail(StatusCodes.Status400BadRequest, "Environment must be TEST or LIVE.");

        if (apiKey.Length > 256 || apiSecret.Length > 512)
            return FiskalyCredentialCommandResult.Fail(StatusCodes.Status400BadRequest, "Fiskaly credentials are too long.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantFiskalyCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null && (apiKey.Length == 0 || apiSecret.Length == 0))
            return FiskalyCredentialCommandResult.Fail(StatusCodes.Status400BadRequest, "Fiskaly API key and secret are required.");

        if (apiKey.Length == 0 && row is not null && string.IsNullOrWhiteSpace(row.ApiKeyProtected))
            return FiskalyCredentialCommandResult.Fail(StatusCodes.Status400BadRequest, "Fiskaly API key is required.");

        var now = DateTime.UtcNow;
        var oldHint = row?.ApiKeyHint;
        var oldScu = row?.ScuId;
        if (row is null)
        {
            row = new TenantFiskalyCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAt = now,
                IsActive = true,
            };
            db.TenantFiskalyCredentials.Add(row);
        }

        if (apiKey.Length > 0)
        {
            row.ApiKeyProtected = _protector.Protect(apiKey);
            row.ApiKeyHint = FiskalyCredentialHints.FromApiKey(apiKey);
        }

        if (apiSecret.Length > 0)
            row.ApiSecretProtected = _protector.Protect(apiSecret);

        row.ScuId = scuGuid.ToString("D");
        row.Environment = environment;
        row.IsActive = true;
        row.UpdatedAt = now;
        row.UpdatedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId.Trim();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _resolver.Invalidate(tenantId);
        _tokenCache.Clear(tenantId);

        await _auditLog.LogSystemOperationAsync(
                action: "FISKALY_CREDENTIALS_SET",
                entityType: "TenantFiskalyCredential",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: "Manager",
                description: $"Fiskaly credentials updated. apiKeyHint={row.ApiKeyHint} scuId={row.ScuId}",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FiskalySettingsChanged,
                entityId: row.Id,
                tenantId: tenantId,
                oldValues: new { ApiKeyHint = oldHint, ScuId = oldScu },
                newValues: new
                {
                    ApiKeyHint = row.ApiKeyHint,
                    ScuId = row.ScuId,
                    Environment = row.Environment,
                    IsActive = row.IsActive
                })
            .ConfigureAwait(false);

        return FiskalyCredentialCommandResult.Ok(Map(row));
    }

    internal static FiskalyTenantCredentialStatusDto Map(TenantFiskalyCredential? row)
    {
        var configured = row is not null
            && row.IsActive
            && !string.IsNullOrWhiteSpace(row.ApiKeyProtected)
            && !string.IsNullOrWhiteSpace(row.ApiSecretProtected)
            && !string.IsNullOrWhiteSpace(row.ScuId);
        return new FiskalyTenantCredentialStatusDto
        {
            Configured = configured,
            IsActive = row?.IsActive == true,
            ApiKeyHint = configured ? row!.ApiKeyHint : null,
            ScuId = row?.ScuId,
            Environment = row?.Environment,
            UpdatedAt = row?.UpdatedAt ?? row?.CreatedAt
        };
    }
}

internal static class FiskalyCredentialHints
{
    public static string FromApiKey(string apiKey)
    {
        var trimmed = apiKey.Trim();
        return trimmed.Length <= 4 ? trimmed : trimmed[^4..];
    }
}
