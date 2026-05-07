using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Filters;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Direct controller invocation does not execute MVC filters — disclaimer is enforced by
/// <see cref="RequireDisclaimerAcknowledgmentFilter"/>. Tests here cover export pipeline wiring only.
/// </summary>
public class FiscalExportDisclaimerAckTests
{
    private static FiscalExportController CreateController(
        IFiscalExportService exportSvc,
        IAuditLogService audit,
        IFiscalExportDownloadTicketStore? tickets = null)
    {
        tickets ??= new Mock<IFiscalExportDownloadTicketStore>().Object;
        return new FiscalExportController(
            exportSvc,
            new DisclaimerService(),
            audit,
            tickets,
            NullLogger<FiscalExportController>.Instance);
    }

    [Fact]
    public async Task GetExport_WithHeaderProceeds_ControllerInvokedDirectly_StillRunsExportService()
    {
        var exportSvc = new Mock<IFiscalExportService>();
        exportSvc
            .Setup(s => s.BuildExportAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<bool>(),
                It.IsAny<FiscalExportProfile>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cash register not found for test"));

        var audit = new Mock<IAuditLogService>();

        var controller = CreateController(exportSvc.Object, audit.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[FiscalExportController.DisclaimerAcknowledgedHeaderName] = "true";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.ReportExport),
        }, "mock"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var rid = Guid.NewGuid();
        var result = await controller.GetExport(
            rid,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            includeCsv: false,
            format: "json",
            exportProfile: null,
            lang: "de",
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        exportSvc.Verify(
            s => s.BuildExportAsync(
                rid,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                false,
                FiscalExportProfile.OperationalPreview,
                "de",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
