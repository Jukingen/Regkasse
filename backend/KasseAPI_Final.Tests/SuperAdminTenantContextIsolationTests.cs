using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.ActivityReports;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// P3: SuperAdmin platform ops use route tenantId; missing/invalid tenants → 404.
/// Ambient tenant is not a substitute for validating the target tenant row.
/// </summary>
public sealed class SuperAdminTenantContextIsolationTests
{
    private static AdminTenantsController CreateController(Mock<IAdminTenantService> tenantService)
    {
        var controller = new AdminTenantsController(
            tenantService.Object,
            Mock.Of<IAdminTenantCsvExportService>(),
            Mock.Of<IAdminTenantLicenseService>(),
            Mock.Of<ITenantDeletionService>(),
            Mock.Of<IActivityReportService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development),
            NullLogger<AdminTenantsController>.Instance);

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "super-admin"),
                new Claim(ClaimTypes.Role, Roles.SuperAdmin),
            ],
            authenticationType: "Test")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Fact]
    public async Task SuperAdmin_TenantNotFound_Returns404()
    {
        var missingId = Guid.NewGuid();
        var service = new Mock<IAdminTenantService>(MockBehavior.Strict);
        service.Setup(s => s.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminTenantDetailDto?)null);

        var controller = CreateController(service);

        var result = await controller.GetById(missingId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(notFound.Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task SuperAdmin_Impersonate_RequiresValidTenant()
    {
        var missingId = Guid.NewGuid();
        var service = new Mock<IAdminTenantService>(MockBehavior.Strict);
        service.Setup(s => s.ImpersonateAsync(missingId, "super-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, "Tenant not found."));

        var controller = CreateController(service);

        var result = await controller.Impersonate(missingId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(notFound.Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task SuperAdmin_Impersonate_Succeeds_ForActiveTenant()
    {
        var tenantId = Guid.NewGuid();
        var service = new Mock<IAdminTenantService>(MockBehavior.Strict);
        service.Setup(s => s.ImpersonateAsync(tenantId, "super-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new TenantImpersonationResponseDto(
                    "access-token",
                    3600,
                    "refresh-token",
                    DateTime.UtcNow.AddDays(1),
                    tenantId,
                    "cafe",
                    "Cafe",
                    true),
                (string?)null));

        var audit = new Mock<IAuditLogService>();
        var controller = new AdminTenantsController(
            service.Object,
            Mock.Of<IAdminTenantCsvExportService>(),
            Mock.Of<IAdminTenantLicenseService>(),
            Mock.Of<ITenantDeletionService>(),
            Mock.Of<IActivityReportService>(),
            audit.Object,
            Mock.Of<IHostEnvironment>(e => e.EnvironmentName == Environments.Development),
            NullLogger<AdminTenantsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "super-admin"),
                    new Claim(ClaimTypes.Role, Roles.SuperAdmin),
                ],
                authenticationType: "Test")),
            },
        };

        var result = await controller.Impersonate(tenantId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TenantImpersonationResponseDto>(ok.Value);
        Assert.Equal(tenantId, dto.TenantId);
        Assert.Equal("cafe", dto.TenantSlug);
        service.VerifyAll();
    }
}
