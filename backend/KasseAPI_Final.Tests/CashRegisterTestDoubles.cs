using System.Security.Claims;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Services.Limits;
using Moq;

namespace KasseAPI_Final.Tests;

internal static class CashRegisterTestDoubles
{
    /// <summary>
    /// Permission gate that approves every register operation, for controller tests whose subject is the handler
    /// behaviour rather than authorization. Authorization itself is covered by CashRegisterPermissionServiceTests.
    /// </summary>
    internal static ICashRegisterPermissionService PermissiveRegisterPermissions()
    {
        var permissions = new Mock<ICashRegisterPermissionService>();
        permissions
            .Setup(p => p.CanAssignUserAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterPermissionResult.Allow());
        permissions
            .Setup(p => p.CanOpenAsync(It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterPermissionResult.Allow());
        permissions
            .Setup(p => p.CanCloseAsync(It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterPermissionResult.Allow());
        permissions
            .Setup(p => p.CanViewAsync(It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterPermissionResult.Allow());
        permissions
            .Setup(p => p.CanCreateSonderbelegAsync(
                It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CashRegisterPermissionResult.Allow());
        return permissions.Object;
    }

    /// <summary>Returns default tenant caps so assignment tests are not blocked by a zero mock limit.</summary>
    internal static ITenantLimitService PermissiveTenantLimits()
    {
        var limits = new Mock<ITenantLimitService>();
        limits
            .Setup(s => s.GetLimitValueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, string key, CancellationToken _) =>
                TenantLimits.CreateDefault(Guid.Empty).GetIntLimit(key));
        limits
            .Setup(s => s.CheckLimitAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        limits
            .Setup(s => s.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tenantId, CancellationToken _) => TenantLimits.CreateDefault(tenantId));
        return limits.Object;
    }

    internal static ICashRegisterListEnrichmentService NoOpListEnrichment()
    {
        var enrichment = new Mock<ICashRegisterListEnrichmentService>();
        enrichment
            .Setup(e => e.ApplyAsync(
                It.IsAny<IReadOnlyList<CashRegisterDto>>(),
                It.IsAny<IReadOnlyList<CashRegister>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return enrichment.Object;
    }
}
