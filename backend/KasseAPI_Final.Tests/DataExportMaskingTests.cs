using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataExport;
using KasseAPI_Final.Services.DataRetention;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataExportMaskingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void MapPayment_MasksTseSignatureAndChain()
    {
        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Max",
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = Guid.NewGuid(),
            TseSignature = "header.payload.signature",
            CertificateThumbprint = "ABCDEF",
            PrevSignatureValueUsed = "prev-sig",
            TseTimestamp = DateTime.UtcNow,
        };

        var json = JsonSerializer.Serialize(DataExportMasking.MapPayment(payment), JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(DataExportMasking.Masked, root.GetProperty("tseSignature").GetString());
        Assert.Equal(DataExportMasking.Masked, root.GetProperty("certificateThumbprint").GetString());
        Assert.Equal(DataExportMasking.Masked, root.GetProperty("prevSignatureValueUsed").GetString());
        Assert.Equal(10m, root.GetProperty("totalAmount").GetDecimal());
        Assert.True(root.GetProperty("masked").GetBoolean());
        Assert.True(root.GetProperty("rksv").GetBoolean());
    }

    [Fact]
    public void MapReceipt_MasksQrAndJws()
    {
        var receipt = new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            ReceiptNumber = "R-1",
            IssuedAt = DateTime.UtcNow,
            CashRegisterId = Guid.NewGuid(),
            SubTotal = 8m,
            TaxTotal = 2m,
            GrandTotal = 10m,
            QrCodePayload = "_R1-AT1_...",
            SignatureValue = "sig",
            JwsHeader = "hdr",
            JwsPayload = "pay",
            JwsSignature = "sig2",
        };

        var json = JsonSerializer.Serialize(DataExportMasking.MapReceipt(receipt), JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(DataExportMasking.Masked, root.GetProperty("qrCodePayload").GetString());
        Assert.Equal(DataExportMasking.Masked, root.GetProperty("jwsSignature").GetString());
        Assert.Equal("R-1", root.GetProperty("receiptNumber").GetString());
    }

    [Fact]
    public void MapInvoice_MasksSignatureAndCustomerContact()
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InvoiceNumber = "INV-1",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            Status = InvoiceStatus.Paid,
            Subtotal = 10m,
            TaxAmount = 2m,
            TotalAmount = 12m,
            CompanyName = "Cafe",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
            CashRegisterId = Guid.NewGuid(),
            KassenId = "K1",
            TseSignature = "compact.jws.sig",
            TseTimestamp = DateTime.UtcNow,
            CustomerEmail = "max.mustermann@example.com",
            CustomerPhone = "+43 664 1234567",
            TaxDetails = JsonDocument.Parse("{}"),
        };

        var json = JsonSerializer.Serialize(DataExportMasking.MapInvoice(invoice), JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(DataExportMasking.Masked, root.GetProperty("tseSignature").GetString());
        Assert.Equal("m***@example.com", root.GetProperty("customerEmail").GetString());
        Assert.Equal("***4567", root.GetProperty("customerPhone").GetString());
    }

    [Fact]
    public void ExportDocument_SerializesCanonicalShape()
    {
        var exportedAt = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
        var document = new TenantDataExportDocument
        {
            Tenant = new TenantDataExportTenantSection
            {
                Name = "Cafe Muster GmbH",
                Slug = "cafe-muster",
                ExportedAt = exportedAt,
            },
            Data = new TenantDataExportDataSection
            {
                Products = Array.Empty<object>(),
                Categories = Array.Empty<object>(),
                Customers = Array.Empty<object>(),
                Payments = Array.Empty<object>(),
                Receipts = Array.Empty<object>(),
                Invoices = Array.Empty<object>(),
                Orders = Array.Empty<object>(),
                Vouchers = Array.Empty<object>(),
                Settings = new { companyName = "Cafe Muster GmbH" },
            },
            Rksv = new TenantDataExportRksvSection
            {
                Note = TenantDataExportDocument.RksvRetentionNote,
                RetentionUntil = exportedAt.AddYears(RksvDataRetentionService.RetentionYears),
            },
        };

        var json = JsonSerializer.Serialize(document, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Cafe Muster GmbH", root.GetProperty("tenant").GetProperty("name").GetString());
        Assert.Equal("cafe-muster", root.GetProperty("tenant").GetProperty("slug").GetString());
        Assert.True(root.GetProperty("data").TryGetProperty("products", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("payments", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("receipts", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("invoices", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("orders", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("vouchers", out _));
        Assert.True(root.GetProperty("data").TryGetProperty("settings", out _));
        Assert.Equal(
            TenantDataExportDocument.RksvRetentionNote,
            root.GetProperty("rksv").GetProperty("note").GetString());
        Assert.Equal(
            "2033-07-19T12:00:00Z",
            root.GetProperty("rksv").GetProperty("retentionUntil").GetDateTime().ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}
