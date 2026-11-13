using KasseAPI_Final.Services.Billing;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Billing;

public sealed class TenantInvoiceServiceTests
{
    [Fact]
    public async Task GetInvoicesForTenantAsync_ReturnsOwnInvoices()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenantA = await harness.CreateTestTenantAsync("tenant-a");
        var tenantB = await harness.CreateTestTenantAsync("tenant-b");
        var saleA = await harness.CreateTestSaleAsync(tenantA.Id, priceNet: 100m);
        await harness.CreateTestSaleAsync(tenantB.Id, priceNet: 200m);

        var sut = CreateSut(harness, pdfBytes: [0x25, 0x50, 0x44, 0x46]);
        var result = await sut.GetInvoicesForTenantAsync(tenantA.Id);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.ActiveCount);
        Assert.Equal(0, result.CancelledCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal(saleA.Id, item.Id);
        Assert.Equal(saleA.InvoiceNumber, item.InvoiceNumber);
        Assert.Equal(120.00m, item.AmountGross);
        Assert.Equal(TenantInvoiceStatuses.Paid, item.Status);
        Assert.Equal(saleA.LicenseKey, item.LicenseKey);
        Assert.Equal(saleA.LicensePlan, item.LicensePlan);
        Assert.Equal(item.IssuedAt, item.InvoiceDateUtc);
        Assert.Equal($"/api/admin/billing/tenant-invoices/{saleA.Id:D}/pdf", item.DownloadUrl);
        Assert.Equal(item.DownloadUrl, item.PdfUrl);
        Assert.DoesNotContain(result.Items, i => i.Id != saleA.Id);
    }

    [Fact]
    public async Task GetInvoicesForTenantAsync_CrossTenant_DoesNotLeakOtherSales()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenantA = await harness.CreateTestTenantAsync("iso-a");
        var tenantB = await harness.CreateTestTenantAsync("iso-b");
        var saleB = await harness.CreateTestSaleAsync(tenantB.Id);

        var sut = CreateSut(harness);
        var result = await sut.GetInvoicesForTenantAsync(tenantA.Id);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
        Assert.DoesNotContain(result.Items, i => i.Id == saleB.Id);
    }

    [Fact]
    public async Task GetInvoicesForTenantAsync_DateFilter_ExcludesOutsideRange()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant = await harness.CreateTestTenantAsync("filter-tenant");
        var sale = await harness.CreateTestSaleAsync(tenant.Id);

        var sut = CreateSut(harness);
        var future = DateTime.UtcNow.AddDays(2);
        var result = await sut.GetInvoicesForTenantAsync(tenant.Id, fromUtc: future);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);

        var included = await sut.GetInvoicesForTenantAsync(
            tenant.Id,
            fromUtc: DateTime.UtcNow.AddDays(-1),
            toUtc: DateTime.UtcNow.AddDays(1));
        Assert.Equal(sale.Id, Assert.Single(included.Items).Id);
    }

    [Fact]
    public async Task GetInvoicesForTenantAsync_StatusPaid_ReturnsActiveSales()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant = await harness.CreateTestTenantAsync("status-tenant");
        var active = await harness.CreateTestSaleAsync(tenant.Id);
        var cancelled = await harness.CreateTestSaleAsync(tenant.Id);
        var actor = await harness.CreateTestUserAsync();
        await harness.CreateBillingService().CancelLicenseSaleAsync(
            cancelled.Id,
            new CancelLicenseSaleRequest { CancellationReason = "test cancel" },
            actor);

        var sut = CreateSut(harness);
        var paid = await sut.GetInvoicesForTenantAsync(tenant.Id, status: TenantInvoiceStatuses.Paid);
        Assert.Equal(active.Id, Assert.Single(paid.Items).Id);
        Assert.Equal(TenantInvoiceStatuses.Paid, paid.Items[0].Status);

        var unpaid = await sut.GetInvoicesForTenantAsync(tenant.Id, status: TenantInvoiceStatuses.Unpaid);
        Assert.Empty(unpaid.Items);

        var overdue = await sut.GetInvoicesForTenantAsync(tenant.Id, status: TenantInvoiceStatuses.Overdue);
        Assert.Empty(overdue.Items);

        var cancelledPage = await sut.GetInvoicesForTenantAsync(
            tenant.Id,
            status: TenantInvoiceStatuses.Cancelled);
        Assert.Equal(cancelled.Id, Assert.Single(cancelledPage.Items).Id);
    }

    [Fact]
    public async Task GetInvoicesForTenantAsync_Paginates()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant = await harness.CreateTestTenantAsync("page-tenant");
        await harness.CreateTestSaleAsync(tenant.Id);
        await harness.CreateTestSaleAsync(tenant.Id);
        await harness.CreateTestSaleAsync(tenant.Id);

        var sut = CreateSut(harness);
        var page1 = await sut.GetInvoicesForTenantAsync(tenant.Id, page: 1, pageSize: 2);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(1, page1.Page);
        Assert.Equal(2, page1.PageSize);

        var page2 = await sut.GetInvoicesForTenantAsync(tenant.Id, page: 2, pageSize: 2);
        Assert.Single(page2.Items);
        Assert.Equal(2, page2.Page);
        var ids = page1.Items.Select(i => i.Id).Concat(page2.Items.Select(i => i.Id)).ToHashSet();
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task GetInvoicePdfForTenantAsync_OtherTenant_ThrowsNotFound()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenantA = await harness.CreateTestTenantAsync("pdf-a");
        var tenantB = await harness.CreateTestTenantAsync("pdf-b");
        var saleB = await harness.CreateTestSaleAsync(tenantB.Id);

        var sut = CreateSut(harness);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.GetInvoicePdfForTenantAsync(tenantA.Id, saleB.Id));
    }

    [Fact]
    public async Task GetInvoicePdfForTenantAsync_OwnSale_ReturnsPdf()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var tenant = await harness.CreateTestTenantAsync("pdf-own");
        var sale = await harness.CreateTestSaleAsync(tenant.Id);
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

        var sut = CreateSut(harness, pdfBytes);
        var (pdf, fileName) = await sut.GetInvoicePdfForTenantAsync(tenant.Id, sale.Id);

        Assert.Equal(pdfBytes, pdf);
        Assert.StartsWith($"RE-{sale.InvoiceNumber}-", fileName, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", fileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetInvoicesForTenantAsync_EmptyTenantId_ReturnsEmpty()
    {
        var harness = await BillingServiceTestHarness.CreateAsync();
        await using var _ = harness;

        var sut = CreateSut(harness);
        var result = await sut.GetInvoicesForTenantAsync(Guid.Empty);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    private static TenantInvoiceService CreateSut(
        BillingServiceTestHarness harness,
        byte[]? pdfBytes = null)
    {
        var pdf = new Mock<IInvoicePdfGenerator>();
        pdf.Setup(x => x.GenerateInvoicePdfAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes ?? [0x25, 0x50, 0x44, 0x46]);

        var (db, _) = harness.CreateDbContextPair();
        return new TenantInvoiceService(db, pdf.Object);
    }
}
