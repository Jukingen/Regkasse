using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.LicenseTest;

public sealed class LicenseTestService : ILicenseTestService
{
    private static readonly Regex DeploymentLicenseKeyRegex = new(
        @"^REGK-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly ILicenseService _licenseService;
    private readonly ILicenseStorageService _storage;
    private readonly IDevelopmentModeService _developmentModeService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LicenseTestService> _logger;

    public LicenseTestService(
        AppDbContext db,
        ILicenseService licenseService,
        ILicenseStorageService storage,
        IDevelopmentModeService developmentModeService,
        IHostEnvironment hostEnvironment,
        ILogger<LicenseTestService> logger)
    {
        _db = db;
        _licenseService = licenseService;
        _storage = storage;
        _developmentModeService = developmentModeService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<LicenseTestSnapshotDto> GetSnapshotAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentHost();
        return await BuildSnapshotAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseTestSnapshotDto> SetTenantExpiryAsync(
        LicenseTestTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentHost();

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            throw new KeyNotFoundException("Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            throw new InvalidOperationException("Deleted tenants cannot be modified.");

        var expiry = ResolveTargetExpiryUtc(request);
        await ApplyTenantTestExpiryOverrideAsync(tenant, expiry, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "License test: tenant {TenantId} ({Slug}) valid_until set to {Expiry:o}",
            tenant.Id,
            tenant.Slug,
            expiry);

        return await BuildSnapshotAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseTestSnapshotDto> SetDeploymentExpiryAsync(
        LicenseTestSetExpiryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentHost();

        var expiry = ResolveTargetExpiryUtc(request);
        await ApplyDeploymentExpiryAsync(expiry, cancellationToken).ConfigureAwait(false);
        _licenseService.EvaluateOnStartup();

        _logger.LogInformation("License test: deployment expiry set to {Expiry:o}", expiry);
        return await BuildSnapshotAsync(null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseTestSnapshotDto> UpdateAsync(
        LicenseTestRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentHost();

        if (!request.ValidUntil.HasValue)
            throw new InvalidOperationException("ValidUntil is required.");

        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            throw new KeyNotFoundException("Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            throw new InvalidOperationException("Deleted tenants cannot be modified.");

        var expiry = DateTime.SpecifyKind(request.ValidUntil.Value, DateTimeKind.Utc);
        await ApplyTenantTestExpiryOverrideAsync(tenant, expiry, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "License test update: tenant {TenantId} ({Slug}) valid_until={Expiry:o}, key={KeyPrefix}…",
            tenant.Id,
            tenant.Slug,
            expiry,
            tenant.LicenseKey is { Length: > 0 } key ? key[..Math.Min(12, key.Length)] : "(none)");

        return await BuildSnapshotAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LicenseTestSnapshotDto> ApplyScenarioAsync(
        LicenseTestScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopmentHost();

        var expiry = ResolveScenarioExpiryUtc(request.Scenario);

        _ = new LicenseTestSetExpiryRequest { ValidUntilUtc = expiry };

        if (request.Scope is LicenseTestScope.Tenant or LicenseTestScope.Both)
        {
            if (!request.TenantId.HasValue || request.TenantId.Value == Guid.Empty)
                throw new InvalidOperationException("tenantId is required when scope includes tenant.");

            await SetTenantExpiryAsync(
                    new LicenseTestTenantRequest
                    {
                        TenantId = request.TenantId.Value,
                        ValidUntilUtc = expiry,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (request.Scope is LicenseTestScope.Deployment or LicenseTestScope.Both)
        {
            await ApplyDeploymentExpiryAsync(expiry, cancellationToken).ConfigureAwait(false);
            _licenseService.EvaluateOnStartup();
        }

        return await BuildSnapshotAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyDeploymentExpiryAsync(DateTime expiryUtc, CancellationToken cancellationToken)
    {
        var persisted = _storage.LoadLicenseFromFile() ?? new LicensePersistedState
        {
            FirstRunUtc = DateTime.UtcNow,
        };

        var normalizedKey = persisted.LicenseKey?.Trim().ToUpperInvariant();
        var hasPaidKey = !string.IsNullOrEmpty(normalizedKey)
            && DeploymentLicenseKeyRegex.IsMatch(normalizedKey);

        if (hasPaidKey)
        {
            persisted.OfflineJwt = null;
            persisted.KeyOnlyPaidValidUntilUtc = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc);
            _storage.SaveLicenseToFile(persisted);

            var machine = _storage.MachineHashHex;
            var activations = await _db.ActivatedLicenses
                .Where(a => a.IsActive)
                .Where(a => a.MachineFingerprint == null || a.MachineFingerprint == machine)
                .Where(a => EF.Functions.ILike(a.LicenseKey, normalizedKey!))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in activations)
            {
                row.ValidUntilUtc = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc);
                row.LastSeenAtUtc = DateTime.UtcNow;
            }

            var issuedRows = await _db.IssuedLicenses
                .Where(il => EF.Functions.ILike(il.LicenseKey, normalizedKey!))
                .Where(il => !il.IsDeleted)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in issuedRows)
            {
                row.ExpiryAtUtc = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc);
            }

            if (activations.Count > 0 || issuedRows.Count > 0)
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((expiryUtc - DateTime.UtcNow).TotalDays));
        var trialDays = LicenseService.TrialDays;
        persisted.FirstRunUtc = DateTime.SpecifyKind(
            DateTime.UtcNow.AddDays(-(trialDays - daysRemaining)),
            DateTimeKind.Utc);
        _storage.SaveLicenseToFile(persisted);
    }

    private async Task<LicenseTestSnapshotDto> BuildSnapshotAsync(
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        LicenseTestTenantStatusDto? tenantDto = null;
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var tenant = await _db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (tenant != null)
            {
                // Use persisted tenant row math — not GetLicenseStatusAsync, which returns 999 in Development
                // when LicenseEnforcementPolicy disables enforcement (test panel must reflect QA assignments).
                var info = LicenseService.BuildTenantLicenseStatusInfo(tenant);
                var now = DateTime.UtcNow;
                tenantDto = new LicenseTestTenantStatusDto(
                    tenant.Id,
                    tenant.Slug,
                    tenant.Name,
                    tenant.LicenseKey,
                    tenant.LicenseValidUntilUtc,
                    TenantLicenseOverviewStatusMapper.ResolveStatus(
                        tenant.LicenseValidUntilUtc,
                        tenant.LicenseKey,
                        now),
                    info.DaysRemaining,
                    info.DaysOverdue,
                    info.IsActive,
                    info.IsInGracePeriod,
                    info.CanAccess,
                    info.CanTransact,
                    info.StatusMessage);
            }
        }

        var deploymentStatus = await _licenseService
            .GetCurrentDeploymentStatusAsync(cancellationToken)
            .ConfigureAwait(false);

        var persisted = _storage.LoadLicenseFromFile();
        var deploymentMode = ResolveDeploymentMode(deploymentStatus, persisted);

        var bypassActive = _developmentModeService.ShouldBypassLicense();

        return new LicenseTestSnapshotDto(
            tenantDto,
            new LicenseTestDeploymentStatusDto(
                deploymentStatus.IsValid,
                deploymentStatus.IsTrial,
                deploymentStatus.IsExpired,
                deploymentStatus.DaysRemaining,
                deploymentStatus.ExpiryDate,
                persisted?.LicenseKey,
                deploymentStatus.IsDevelopmentBypass,
                deploymentMode),
            bypassActive,
            DateTime.UtcNow);
    }

    private static string ResolveDeploymentMode(
        LicenseStatusResponse status,
        LicensePersistedState? persisted)
    {
        if (status.IsDevelopmentBypass)
            return "development_bypass";

        var key = persisted?.LicenseKey?.Trim();
        if (!string.IsNullOrEmpty(key) && DeploymentLicenseKeyRegex.IsMatch(key.ToUpperInvariant()))
            return "paid";

        if (status.IsTrial)
            return "trial";

        return status.IsExpired ? "expired" : "unknown";
    }

    private static DateTime ResolveTargetExpiryUtc(LicenseTestSetExpiryRequest request)
    {
        if (request.SetExpired)
            return DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Utc);

        if (request.SetActive)
            return DateTime.SpecifyKind(DateTime.UtcNow.AddDays(30), DateTimeKind.Utc);

        if (request.ValidUntilUtc.HasValue)
            return DateTime.SpecifyKind(request.ValidUntilUtc.Value, DateTimeKind.Utc);

        throw new InvalidOperationException("Provide validUntilUtc, setActive, or setExpired.");
    }

    private static DateTime ResolveScenarioExpiryUtc(LicenseTestScenario scenario) =>
        scenario switch
        {
            LicenseTestScenario.Days1 => DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Utc),
            LicenseTestScenario.Days7 => DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc),
            LicenseTestScenario.Days30 => DateTime.SpecifyKind(DateTime.UtcNow.AddDays(30), DateTimeKind.Utc),
            LicenseTestScenario.Expired => DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario."),
        };

    /// <summary>
    /// Replaces mandant expiry for QA (never extends on top of existing rows). Clears billing/grace state
    /// so issued-license sync cannot immediately restore a long-lived key.
    /// </summary>
    private async Task ApplyTenantTestExpiryOverrideAsync(
        Tenant tenant,
        DateTime expiryUtc,
        CancellationToken cancellationToken)
    {
        var expiry = DateTime.SpecifyKind(expiryUtc, DateTimeKind.Utc);
        tenant.LicenseValidUntilUtc = expiry;
        tenant.LicenseKey = "TEST-" + Guid.NewGuid().ToString("N");
        tenant.LicenseGracePeriodStartedAt = expiry <= DateTime.UtcNow ? DateTime.UtcNow : null;
        tenant.LicenseGracePeriodUsedDays = 0;
        tenant.CurrentLicenseSaleId = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void EnsureDevelopmentHost()
    {
        if (!_hostEnvironment.IsDevelopment())
            throw new InvalidOperationException("License test tools are only available in Development.");
    }
}
