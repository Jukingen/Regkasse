using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;

namespace KasseAPI_Final.Tests;

public sealed class InvoicePdfServiceTests
{
    [Fact]
    public async Task GenerateInvoicePdfAsync_Throws_WhenInvoiceMissing()
    {
        var db = CreateDb();
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.GenerateInvoicePdfAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GenerateInvoicePdfAsync_ReturnsPdf_ForPersistedInvoice()
    {
        var db = CreateDb();
        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(CreateSampleInvoice(invoiceId));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var pdf = await sut.GenerateInvoicePdfAsync(invoiceId);

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 100);
        Assert.Equal(0x25, pdf[0]); // %
        Assert.Equal(0x50, pdf[1]); // P
        Assert.Equal(0x44, pdf[2]); // D
        Assert.Equal(0x46, pdf[3]); // F
    }

    [Fact]
    public async Task ResendInvoiceEmailAsync_ReturnsFalse_WhenNoRecipient()
    {
        var db = CreateDb();
        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(CreateSampleInvoice(invoiceId, customerEmail: null));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, emailConfigured: true);
        var sent = await sut.ResendInvoiceEmailAsync(invoiceId, null);

        Assert.False(sent);
    }

    [Fact]
    public async Task ResendInvoiceEmailAsync_SendsAndAudits_WhenRecipientProvided()
    {
        var db = CreateDb();
        var invoiceId = Guid.NewGuid();
        db.Invoices.Add(CreateSampleInvoice(invoiceId, customerEmail: "kunde@example.com"));
        await db.SaveChangesAsync();

        var email = new Mock<IInvoiceEmailService>();
        email.Setup(x => x.IsConfigured).Returns(true);
        email.Setup(x => x.TrySendInvoiceAsync(
                It.IsAny<Invoice>(),
                It.IsAny<byte[]>(),
                "kunde@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogSystemOperationAsync(
                AuditLogActions.INVOICE_RESENT,
                AuditLogEntityTypes.INVOICE,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                AuditEventType.InvoiceResent,
                invoiceId,
                It.IsAny<Guid?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        var sut = CreateSut(db, email: email.Object, audit: audit.Object);
        var sent = await sut.ResendInvoiceEmailAsync(invoiceId, null);

        Assert.True(sent);
        email.Verify(x => x.TrySendInvoiceAsync(
            It.IsAny<Invoice>(),
            It.IsAny<byte[]>(),
            "kunde@example.com",
            It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.LogSystemOperationAsync(
            AuditLogActions.INVOICE_RESENT,
            AuditLogEntityTypes.INVOICE,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.InvoiceResent,
            invoiceId,
            It.IsAny<Guid?>()), Times.Once);
    }

    private static InvoicePdfService CreateSut(
        AppDbContext db,
        bool emailConfigured = false,
        IInvoiceEmailService? email = null,
        IAuditLogService? audit = null)
    {
        email ??= CreateEmailMock(emailConfigured).Object;
        audit ??= Mock.Of<IAuditLogService>();

        var invoiceService = new InvoiceService(
            db,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
            {
                CompanyName = "Test GmbH",
                TaxNumber = "ATU12345678",
                Street = "Hauptstrasse 1",
                ZipCode = "1010",
                City = "Wien",
            }),
            TenantTestDoubles.PrimaryTenantResolver);

        return new InvoicePdfService(
            db,
            invoiceService,
            email,
            audit,
            new HttpContextAccessor(),
            NullLogger<InvoicePdfService>.Instance);
    }

    private static Mock<IInvoiceEmailService> CreateEmailMock(bool configured)
    {
        var mock = new Mock<IInvoiceEmailService>();
        mock.Setup(x => x.IsConfigured).Returns(configured);
        return mock;
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.TenantId).Returns(LegacyDefaultTenantIds.Primary);

        var db = new AppDbContext(options, tenantAccessor.Object);
        TenantTestDoubles.EnsureDefaultTenant(db);
        return db;
    }

    private static Invoice CreateSampleInvoice(Guid id, string? customerEmail = "kunde@example.com")
    {
        var items = JsonDocument.Parse("""
            [
              {"productId":"00000000-0000-0000-0000-000000000001","productName":"Kaffee","quantity":2,"unitPrice":3.50,"totalPrice":7.00,"taxType":1,"taxRate":0.20,"taxAmount":1.17,"lineNet":5.83}
            ]
            """);

        return new Invoice
        {
            Id = id,
            TenantId = LegacyDefaultTenantIds.Primary,
            InvoiceNumber = "R-1001",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            Status = InvoiceStatus.Paid,
            Subtotal = 5.83m,
            TaxAmount = 1.17m,
            TotalAmount = 7.00m,
            PaidAmount = 7.00m,
            RemainingAmount = 0,
            CustomerName = "Max Mustermann",
            CustomerEmail = customerEmail,
            CompanyName = "Test GmbH",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
            TseSignature = "sig",
            KassenId = "KASSA-1",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = Guid.NewGuid(),
            InvoiceItems = items,
            TaxDetails = JsonDocument.Parse("{}"),
            IsActive = true,
        };
    }
}
