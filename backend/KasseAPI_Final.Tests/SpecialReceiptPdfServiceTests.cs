using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SpecialReceiptPdfServiceTests
{
    [Theory]
    [InlineData(RksvSpecialReceiptKinds.Monatsbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Nullbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Startbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Schlussbeleg)]
    public void Generate_ProducesValidPdf_WithRksvRequiredLayout(string receiptType)
    {
        var signature = "eyJhbGciOiJFUzI1NiJ9.eyJ0ZXN0IjoidmFsdWUifQ.signature-body-full";
        var pdf = SpecialReceiptPdfService.Generate(new SpecialReceiptPdfData
        {
            CompanyName = "Musterfirma GmbH",
            CompanyAddress = "Hauptstrasse 1, 8010 Graz",
            CompanyVatId = "ATU12345678",
            ReceiptType = receiptType,
            CashRegisterId = Guid.NewGuid().ToString("N"),
            RegisterNumber = "KASSE-01",
            ReceiptNumber = "BELEG-42",
            IssuedAt = new DateTime(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc),
            TotalAmount = 0m,
            PaymentMethod = "Cash",
            TseSignature = signature,
            TseSignatureTimestamp = "15.07.2026 14:30:00",
            RksvFooterLabel = "RKSV-konform",
        });

        Assert.True(pdf.Length > 400);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void Generate_WithQrFallback_StillProducesValidPdf()
    {
        var pdf = SpecialReceiptPdfService.Generate(
            new SpecialReceiptPdfData
            {
                CompanyName = "Firma",
                CompanyAddress = "Wien",
                CompanyVatId = "ATU11111111",
                ReceiptType = RksvSpecialReceiptKinds.Nullbeleg,
                CashRegisterId = "abc",
                RegisterNumber = "K1",
                ReceiptNumber = "N-1",
                IssuedAt = DateTime.UtcNow,
                TseSignature = "sig",
                RksvFooterLabel = "RKSV-konform",
            },
            qrPng: null,
            qrFallbackText: "_R1-AT1_FALLBACK_PAYLOAD");

        Assert.True(pdf.Length > 400);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task ReceiptPdfService_SpecialReceipt_UsesEnrichedLayout_NotMinimalReprint()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SpecialReceiptPdf_{Guid.NewGuid():N}")
            .Options;
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var context = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureDefaultTenant(context);

        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var signature = "compact.jws.signature-for-monatsbeleg-pdf-test-full-value";

        context.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = registerId,
            RegisterNumber = "KASSE-PDF",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "cashier1",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(),
            Steuernummer = "ATU87654321",
            CompanyName = "Snapshot GmbH",
            CompanyAddress = "Snapshotgasse 9, 1010 Wien",
            CashRegisterId = registerId,
            TseSignature = signature,
            TseTimestamp = DateTime.UtcNow,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
            RksvSpecialReceiptYear = 2026,
            RksvSpecialReceiptMonth = 6,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        context.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = paymentId,
            ReceiptNumber = "MB-2026-06",
            IssuedAt = DateTime.UtcNow,
            CashRegisterId = registerId,
            SubTotal = 0,
            TaxTotal = 0,
            GrandTotal = 0,
            QrCodePayload = "_R1-AT1_TEST_QR_PAYLOAD",
            SignatureValue = signature,
            CreatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();

        var qr = new Mock<IQrImageService>();
        qr.Setup(q => q.GetQrPngFromExactPayload(It.IsAny<string?>())).Returns((byte[]?)null);
        qr.Setup(q => q.GenerateQrCodeImage(It.IsAny<string?>())).Returns((byte[]?)null);

        var company = TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
        {
            CompanyName = "Live GmbH",
            TaxNumber = "ATU00000000",
            Street = "Live Strasse 1",
            ZipCode = "1020",
            City = "Wien",
        });

        var svc = new ReceiptPdfService(
            context,
            qr.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            company,
            TenantTestDoubles.ProductionHostEnvironment,
            Options.Create(new TseOptions()),
            NullLogger<ReceiptPdfService>.Instance);

        var specialPdf = await svc.GeneratePdfAsync(paymentId, includeReprintWatermark: false);

        // Baseline: same service for a normal receipt is the minimal thermal layout (smaller / different path).
        // Special layout always includes company header + RKSV footer blocks → larger document.
        Assert.True(specialPdf.Length > 800);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(specialPdf, 0, 4));

        var (name, address, vat) = CompanyProfileMapper.ResolveForDisplay(
            await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == paymentId),
            await company.GetCompanyProfileAsync());
        Assert.Equal("Snapshot GmbH", name);
        Assert.Equal("Snapshotgasse 9, 1010 Wien", address);
        Assert.Equal("ATU87654321", vat);
    }
}
