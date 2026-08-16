using System.Reflection;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminTenantBillingInvoicesControllerTests
{
    [Fact]
    public void TenantInvoices_RequiresAuth()
    {
        var type = typeof(AdminTenantBillingInvoicesController);
        Assert.Contains(type.GetCustomAttributes<AuthorizeAttribute>(inherit: true), _ => true);

        var perms = type.GetCustomAttributes<HasPermissionAttribute>(inherit: true)
            .Select(a => a.Permission)
            .ToList();
        Assert.Contains(AppPermissions.LicenseManage, perms);
        Assert.DoesNotContain(AppPermissions.SystemCritical, perms);
    }

    [Fact]
    public async Task List_WithoutAmbientTenant_ReturnsNotFound()
    {
        var invoices = new Mock<ITenantInvoiceService>(MockBehavior.Strict);
        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, (Guid?)null);

        var controller = CreateController(invoices.Object, accessor.Object);
        var result = await controller.GetTenantInvoices();

        Assert.IsType<NotFoundResult>(result);
        invoices.Verify(
            x => x.GetInvoicesForTenantAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task List_WithAmbientTenant_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var invoices = new Mock<ITenantInvoiceService>();
        invoices
            .Setup(x => x.GetInvoicesForTenantAsync(
                tenantId, 1, 20, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantInvoiceListResponse
            {
                Items = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20,
            });

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, tenantId);

        var controller = CreateController(invoices.Object, accessor.Object);
        var result = await controller.GetTenantInvoices();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<TenantInvoiceListResponse>(ok.Value);
        Assert.Equal(0, body.TotalCount);
    }

    [Fact]
    public async Task List_PassesPaginationStatusAndDateFilters()
    {
        var tenantId = Guid.NewGuid();
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var invoices = new Mock<ITenantInvoiceService>();
        invoices
            .Setup(x => x.GetInvoicesForTenantAsync(
                tenantId, 2, 10, "paid", from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantInvoiceListResponse
            {
                Items = [],
                TotalCount = 0,
                Page = 2,
                PageSize = 10,
            });

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, tenantId);

        var controller = CreateController(invoices.Object, accessor.Object);
        var result = await controller.GetTenantInvoices(
            page: 2,
            pageSize: 10,
            status: "paid",
            fromDate: from,
            toDate: to);

        Assert.IsType<OkObjectResult>(result);
        invoices.Verify(
            x => x.GetInvoicesForTenantAsync(
                tenantId, 2, 10, "paid", from, to, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DownloadPdf_CrossTenant_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherInvoiceId = Guid.NewGuid();
        var invoices = new Mock<ITenantInvoiceService>();
        invoices
            .Setup(x => x.GetInvoicePdfForTenantAsync(tenantId, otherInvoiceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Invoice not found."));

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, tenantId);

        var controller = CreateController(invoices.Object, accessor.Object);
        var result = await controller.GetTenantInvoicePdf(otherInvoiceId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DownloadPdf_OwnInvoice_ReturnsFile()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var invoices = new Mock<ITenantInvoiceService>();
        invoices
            .Setup(x => x.GetInvoicePdfForTenantAsync(tenantId, invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((pdfBytes, "RE-2026-001-dev.pdf"));

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.SetupProperty(a => a.TenantId, tenantId);

        var controller = CreateController(invoices.Object, accessor.Object);
        var result = await controller.GetTenantInvoicePdf(invoiceId);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("RE-2026-001-dev.pdf", file.FileDownloadName);
        Assert.Equal(pdfBytes, file.FileContents);
    }

    private static AdminTenantBillingInvoicesController CreateController(
        ITenantInvoiceService invoices,
        ICurrentTenantAccessor accessor)
    {
        return new AdminTenantBillingInvoicesController(invoices, accessor)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }
}
