using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreServiceTests
{
    private readonly RestoreService _sut = new();

    [Fact]
    public void EvaluateSameTenant_allows_when_backup_and_operating_tenant_match()
    {
        var tenantId = Guid.NewGuid();
        var result = _sut.EvaluateSameTenant(tenantId, tenantId);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EvaluateSameTenant_rejects_cross_tenant()
    {
        var result = _sut.EvaluateSameTenant(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.Succeeded);
        Assert.Equal(RestoreService.CrossTenantCode, result.Code);
        Assert.Contains("RKSV", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateSameTenant_allows_deployment_wide_backup_with_operating_tenant()
    {
        // Shared instance dump (tenant_id null) — platform Super Admin may drill under a tenant context.
        var result = _sut.EvaluateSameTenant(null, Guid.NewGuid());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EvaluateSameTenant_allows_tenant_scoped_backup_without_operating_tenant()
    {
        // Platform host Super Admin (no ambient tenant) may request validation restore of a labeled run.
        var result = _sut.EvaluateSameTenant(Guid.NewGuid(), null);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EnsureCanStartValidationRestore_rejects_production_path()
    {
        var backup = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        };

        var result = _sut.EnsureCanStartValidationRestore(backup, operatingTenantId: null, validationOnly: false);
        Assert.False(result.Succeeded);
        Assert.Equal(RestoreService.ProductionRestoreCode, result.Code);
    }

    [Fact]
    public void EnsureCanStartValidationRestore_rejects_non_succeeded_backup()
    {
        var backup = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Failed,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        };

        var result = _sut.EnsureCanStartValidationRestore(backup, null, validationOnly: true);
        Assert.False(result.Succeeded);
        Assert.Equal(RestoreService.BackupNotSucceededCode, result.Code);
    }

    [Fact]
    public void EnsureCanStartValidationRestore_rejects_tenant_zip_package()
    {
        var tenantId = Guid.NewGuid();
        var backup = new BackupRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "TenantLogical",
            RequestedAt = DateTime.UtcNow
        };

        var result = _sut.EnsureCanStartValidationRestore(backup, tenantId, validationOnly: true);
        Assert.False(result.Succeeded);
        Assert.Equal(ComplianceCheckService.TenantPackageRestoreCode, result.Code);
    }

    [Fact]
    public void EnsureCanStartValidationRestore_ok_for_validation_same_tenant()
    {
        var tenantId = Guid.NewGuid();
        var backup = new BackupRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow
        };

        var result = _sut.EnsureCanStartValidationRestore(backup, tenantId, validationOnly: true);
        Assert.True(result.Succeeded);
    }
}
