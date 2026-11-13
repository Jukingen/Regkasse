using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DeploymentComplianceServiceTests
{
    private static (IDbContextFactory<AppDbContext> Factory, string DbName) CreateFactory()
    {
        var dbName = $"Compliance_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        return (factory.Object, dbName);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static DeploymentComplianceService CreateService(
        IDbContextFactory<AppDbContext> factory,
        out Mock<IDeploymentAuditService> audit)
    {
        audit = new Mock<IDeploymentAuditService>();
        return new DeploymentComplianceService(
            factory,
            audit.Object,
            NullLogger<DeploymentComplianceService>.Instance);
    }

    private static DeploymentComplianceChecklistDto CompleteChecklist() => new()
    {
        DepExportTested = true,
        TseSignatureTested = true,
        FinanzOnlineTestSubmission = true,
        NtpTimeSyncChecked = true,
        TenantIsolationVerified = true,
    };

    [Fact]
    public async Task SignOffAsync_RequiresCompleteChecklist()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, out _);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SignOffAsync(
                new DeploymentComplianceSignoffRequest
                {
                    ImageTag = "sha-abc1234",
                    Checklist = new DeploymentComplianceChecklistDto { DepExportTested = true },
                },
                "user-1",
                Roles.ComplianceOfficer,
                "Officer"));
    }

    [Fact]
    public async Task SignOffAsync_PersistsAndPassesGate()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, out var audit);

        var dto = await svc.SignOffAsync(
            new DeploymentComplianceSignoffRequest
            {
                ImageTag = "sha-abc1234",
                GitSha = "abc1234",
                Stage = "production",
                Checklist = CompleteChecklist(),
                Notes = "OK for release",
                ValidHours = 48,
            },
            "user-1",
            Roles.ComplianceOfficer,
            "Officer");

        Assert.True(dto.IsValid);
        Assert.Equal("sha-abc1234", dto.ImageTag);
        audit.Verify(
            a => a.LogComplianceApprovedAsync(It.IsAny<DeploymentComplianceSignoff>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var gate = await svc.GetGateStatusAsync("sha-abc1234");
        Assert.True(gate.GatePassed);
        Assert.True(gate.SignoffValid);
        Assert.Empty(gate.MissingChecklistItems);
    }

    [Fact]
    public async Task GetGateStatusAsync_FailsWithoutSignoff()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, out _);

        var gate = await svc.GetGateStatusAsync("missing-tag");
        Assert.False(gate.GatePassed);
        Assert.False(gate.SignoffPresent);
        Assert.Contains("depExportTested", gate.MissingChecklistItems);
    }
}

public sealed class ComplianceOfficerRoleTests
{
    [Fact]
    public void Canonical_IncludesComplianceOfficer()
    {
        Assert.Contains(Roles.ComplianceOfficer, Roles.Canonical);
    }

    [Fact]
    public void Matrix_GrantsDeploymentApprove_NotSystemCritical()
    {
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.ComplianceOfficer, AppPermissions.DeploymentApprove));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.ComplianceOfficer, AppPermissions.AuditView));
        Assert.False(RolePermissionMatrix.RoleHasPermission(Roles.ComplianceOfficer, AppPermissions.SystemCritical));
        Assert.True(RolePermissionMatrix.RoleHasPermission(Roles.SuperAdmin, AppPermissions.DeploymentApprove));
    }

    [Fact]
    public void ClientAppPolicy_AllowsAdmin_NotPos()
    {
        Assert.True(ClientAppPolicy.IsRoleAllowedForApp(ClientAppPolicy.Admin, Roles.ComplianceOfficer));
        Assert.False(ClientAppPolicy.IsRoleAllowedForApp(ClientAppPolicy.Pos, Roles.ComplianceOfficer));
    }
}
