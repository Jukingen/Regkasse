using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Fiskaly;

public sealed class FiskalySettingsService : IFiskalySettingsService
{
    private readonly IOptionsMonitor<FiskalyOptions> _options;
    private readonly FiskalyEnabledOverrideCache _enabledCache;
    private readonly IFiskalyClient _client;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentTenantAccessor? _tenantAccessor;
    private readonly ILogger<FiskalySettingsService> _logger;

    public FiskalySettingsService(
        IOptionsMonitor<FiskalyOptions> options,
        FiskalyEnabledOverrideCache enabledCache,
        IFiskalyClient client,
        IDbContextFactory<AppDbContext> dbFactory,
        IAuditLogService auditLog,
        ILogger<FiskalySettingsService> logger,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        _options = options;
        _enabledCache = enabledCache;
        _client = client;
        _dbFactory = dbFactory;
        _auditLog = auditLog;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    public FiskalySettingsDto GetSettings()
    {
        var opts = _options.CurrentValue;
        var resolved = _enabledCache.Resolve(_tenantAccessor?.TenantId);
        return MapSettings(opts, resolved);
    }

    public async Task<FiskalyStatusDto> GetStatusAsync(
        bool probeAuthentication = true,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var resolved = _enabledCache.Resolve(_tenantAccessor?.TenantId);
        var enabled = opts.IsEffectivelyEnabled(resolved.Overlay);
        var status = new FiskalyStatusDto
        {
            IsEnabled = enabled,
            IsConfigured = opts.HasApiCredentials,
            Environment = opts.ResolveEnvironment(),
            IsAuthenticated = false,
            Source = resolved.Source
        };

        if (!probeAuthentication || !enabled || !opts.HasApiCredentials)
            return status;

        try
        {
            var auth = await _client.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
            status.IsAuthenticated = auth.Success;
            status.LastCheck = DateTime.UtcNow;
            if (!auth.Success)
                status.Error = "Authentication did not succeed.";
        }
        catch (Exception ex)
        {
            status.LastCheck = DateTime.UtcNow;
            status.Error = Sanitize(ex.Message);
            _logger.LogWarning(ex, "Fiskaly status authentication probe failed.");
        }

        return status;
    }

    public async Task<FiskalySettingsDto> UpdateEnabledAsync(
        bool enabled,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var tenantId = _tenantAccessor?.TenantId;
        var oldResolved = _enabledCache.Resolve(tenantId);
        var oldEnabled = opts.IsEffectivelyEnabled(oldResolved.Overlay);
        var source = tenantId is null ? "global_override" : "tenant_override";

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantSettings
            .FirstOrDefaultAsync(
                s => s.TenantId == tenantId && s.Key == FiskalyEnabledOverrideCache.SettingsKey,
                cancellationToken)
            .ConfigureAwait(false);

        var oldValue = row?.Value;
        if (row is null)
        {
            row = new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = FiskalyEnabledOverrideCache.SettingsKey,
                Value = enabled ? "true" : "false",
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = actorUserId,
            };
            db.TenantSettings.Add(row);
        }
        else
        {
            row.Value = enabled ? "true" : "false";
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = actorUserId;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _enabledCache.SetOverride(enabled, tenantId);

        await _auditLog.LogSystemOperationAsync(
                action: "FISKALY_ENABLED_SET",
                entityType: "TenantSetting",
                userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                userRole: tenantId is null ? "SuperAdmin" : "Manager",
                description: $"Fiskaly Enabled set to {enabled} (was {oldEnabled})",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FiskalySettingsChanged,
                entityId: row.Id,
                tenantId: tenantId,
                oldValues: oldValue is null ? new { Enabled = oldEnabled } : new { Value = oldValue, Enabled = oldEnabled },
                newValues: new { Enabled = enabled, Source = source, TenantId = tenantId })
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Fiskaly Enabled overlay set to {Enabled} by {Actor} (tenant={TenantId})",
            enabled,
            actorUserId,
            tenantId?.ToString("D") ?? "global");
        return MapSettings(_options.CurrentValue, new FiskalyEnabledResolution(enabled, source));
    }

    private static FiskalySettingsDto MapSettings(FiskalyOptions opts, FiskalyEnabledResolution resolved) => new()
    {
        Enabled = opts.IsEffectivelyEnabled(resolved.Overlay),
        ConfigEnabled = opts.Enabled,
        OverrideEnabled = resolved.Overlay,
        Environment = opts.ResolveEnvironment(),
        IsConfigured = opts.HasApiCredentials,
        ApiBaseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl) ? string.Empty : opts.BaseUrl,
        Source = resolved.Source
    };

    private static string Sanitize(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "fiskaly request failed.";

        var trimmed = message.Trim();
        if (trimmed.Length > 240)
            trimmed = trimmed[..240] + "…";

        return trimmed
            .Replace("api_secret", "***", StringComparison.OrdinalIgnoreCase)
            .Replace("api_key", "***", StringComparison.OrdinalIgnoreCase);
    }
}
