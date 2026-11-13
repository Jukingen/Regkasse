using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminChQrInvoiceOperatorControllerTests
{
    [Fact]
    public async Task Download_GapsNotAccepted_Returns409WithOutstandingIds()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId, invoiceId, country: "CH");
        var gaps = Gaps(accepted: ["official-swiss-cross"]);
        var controller = Controller(db, tenantId, gaps.Object, new Mock<IQrRechnungBuilder>().Object);

        var result = await controller.DownloadPdf(tenantId, invoiceId, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<ChQrGapsNotAcceptedResponse>(conflict.Value);
        Assert.Equal(AdminChQrInvoiceOperatorController.GapsNotAcceptedCode, body.Code);
        Assert.Contains("pain001", body.OutstandingGapIds);
        Assert.DoesNotContain("official-swiss-cross", body.OutstandingGapIds);
    }

    [Fact]
    public async Task Download_GapsAccepted_ReturnsPdfBytes()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId, invoiceId, country: "CH");
        var gaps = Gaps(accepted: ["official-swiss-cross", "pain001"]);
        var qr = new Mock<IQrRechnungBuilder>();
        qr.Setup(builder => builder.BuildPdfAsync(It.IsAny<QrRechnungRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("%PDF"u8.ToArray());
        var audit = new Mock<IAuditLogService>();
        audit.Setup(item => item.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        var controller = Controller(db, tenantId, gaps.Object, qr.Object, audit.Object);

        var result = await controller.DownloadPdf(tenantId, invoiceId, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("%PDF"u8.ToArray(), file.FileContents);
        audit.Verify(item => item.LogSystemOperationAsync(
            "QR_RECHNUNG_PDF_DOWNLOADED",
            "Invoice",
            It.IsAny<string>(),
            "SuperAdmin",
            It.IsAny<string?>(),
            null,
            AuditLogStatus.Success,
            null,
            null,
            null,
            null,
            null,
            AuditEventType.QrRechnungPdfDownloaded,
            invoiceId,
            tenantId,
            null,
            It.IsAny<object?>(),
            null), Times.Once);
        Assert.Equal(116, (int)AuditEventType.QrRechnungPdfDownloaded);
    }

    [Fact]
    public async Task Upload_WithoutGeneratedPdf_Returns400()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId, invoiceId, country: "CH");
        var controller = Controller(db, tenantId, Gaps(accepted: []).Object, new Mock<IQrRechnungBuilder>().Object);

        var result = await controller.ConfirmUpload(
            tenantId,
            invoiceId,
            new ChQrBankUploadConfirmationRequest("ops", "2026-09-30T08:00:00Z", "BANK-1"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_AfterPdfGenerated_WritesAuditAndReturns200()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId, invoiceId, country: "CH", pdfGenerated: true);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(item => item.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        var controller = Controller(db, tenantId, Gaps(accepted: []).Object, new Mock<IQrRechnungBuilder>().Object, audit.Object);

        var result = await controller.ConfirmUpload(
            tenantId,
            invoiceId,
            new ChQrBankUploadConfirmationRequest("ops", "2026-09-30T08:00:00Z", "BANK-1"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        audit.Verify(item => item.LogSystemOperationAsync(
            "QR_RECHNUNG_BANK_UPLOAD_CONFIRMED",
            "Invoice",
            It.IsAny<string>(),
            "SuperAdmin",
            It.IsAny<string?>(),
            null,
            AuditLogStatus.Success,
            null,
            null,
            null,
            null,
            null,
            AuditEventType.QrRechnungBankUploadConfirmed,
            invoiceId,
            tenantId,
            null,
            It.IsAny<object?>(),
            null), Times.Once);
        Assert.Equal(117, (int)AuditEventType.QrRechnungBankUploadConfirmed);
    }

    [Fact]
    public async Task Download_OtherTenant_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = await SeedAsync(tenantId, invoiceId, country: "CH");
        var controller = Controller(db, otherId, Gaps(accepted: []).Object, new Mock<IQrRechnungBuilder>().Object);

        var result = await controller.DownloadPdf(tenantId, invoiceId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static Mock<IChQrGapAcceptanceService> Gaps(IReadOnlyList<string> accepted)
    {
        var gaps = new Mock<IChQrGapAcceptanceService>();
        gaps.Setup(service => service.KnownGaps).Returns(
        [
            new ChQrKnownGap("official-swiss-cross", false),
            new ChQrKnownGap("pain001", false),
            new ChQrKnownGap("already-present", true),
        ]);
        gaps.Setup(service => service.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChQrGapAcceptance(accepted, "ops", DateTime.UtcNow));
        return gaps;
    }

    private static AdminChQrInvoiceOperatorController Controller(
        AppDbContext db,
        Guid ambientTenantId,
        IChQrGapAcceptanceService gaps,
        IQrRechnungBuilder qr,
        IAuditLogService? audit = null)
    {
        var auditMock = audit ?? new Mock<IAuditLogService>().Object;
        var controller = new AdminChQrInvoiceOperatorController(
            db,
            TenantTestDoubles.TenantAccessorReturning(ambientTenantId),
            gaps,
            qr,
            Options.Create(new QrRechnungOptions()),
            auditMock);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "super-admin")],
                    "test")),
            },
        };
        return controller;
    }

    private static async Task<AppDbContext> SeedAsync(
        Guid tenantId,
        Guid invoiceId,
        string country,
        bool pdfGenerated = false)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ChQrOp_{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "ch", Slug = "ch", IsActive = true });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Cafe Bern",
            CompanyAddress = "Bern",
            CompanyTaxNumber = "CHE123",
            Country = country,
            Currency = "CHF",
            BankAccountNumber = "CH9300762011623852957",
            Language = "de-DE",
            TimeZone = "Europe/Zurich",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
            BusinessHours = new Dictionary<string, string>(),
        });
        db.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            InvoiceNumber = "CH-1",
            InvoiceDate = now,
            DueDate = now,
            Status = InvoiceStatus.Paid,
            Subtotal = 10m,
            TaxAmount = 0.81m,
            TotalAmount = 10.81m,
            PaidAmount = 10.81m,
            RemainingAmount = 0m,
            CompanyName = "Cafe Bern",
            CompanyTaxNumber = "CHE123",
            CompanyAddress = "Bern",
            CreatedAt = now,
            IsActive = true,
        });
        if (pdfGenerated)
        {
            db.AuditLogs.Add(new AuditLog
            {
                TenantId = tenantId,
                EntityId = invoiceId,
                EntityType = "Invoice",
                Action = "QR_RECHNUNG_PDF_GENERATED",
                ActionType = AuditEventType.QrRechnungPdfGenerated,
                UserId = "system",
                UserRole = "system",
                SessionId = "test",
                Status = AuditLogStatus.Success,
                Timestamp = now,
                CreatedAt = now,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync();
        return db;
    }
}
