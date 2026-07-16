using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvReportTemplateTests
{
    [Theory]
    [InlineData(RksvSpecialReceiptKinds.Nullbeleg, RksvReportNames.Nullbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Startbeleg, RksvReportNames.Startbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Monatsbeleg, RksvReportNames.Monatsbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Jahresbeleg, RksvReportNames.Jahresbeleg)]
    [InlineData(RksvSpecialReceiptKinds.Schlussbeleg, RksvReportNames.Schlussbeleg)]
    public void Mapper_ResolvesSpecialReceiptReportNames(string kind, string expectedName)
    {
        var name = RksvReportTemplateMapper.ResolveReceiptReportName(new ReceiptDTO
        {
            RksvSpecialReceiptKind = kind,
        });

        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void Renderer_IncludesUnifiedCloudSections()
    {
        var text = RksvReportTemplateRenderer.Render(new RksvReportTemplate
        {
            CompanyName = "Test GmbH",
            CompanyAddress = "Wien",
            CompanyVatId = "ATU99999999",
            ReportName = RksvReportNames.Startbeleg,
            CashRegisterId = "abc123",
            RegisterNumber = "K1",
            CashierName = "Max Mustermann",
            ShiftNumber = "A1B2C3D4",
            TotalGross = 0m,
            TotalNet = 0m,
            TaxAmount = 0m,
            TseProvider = "fiskaly Cloud-HSM",
            DepExportStatus = "Ausstehend",
            HasStartbeleg = true,
            QrCode = "QR_TEST",
            RksvFooter = "RKSV-konform",
            TseSignatureVerified = true,
        });

        Assert.Contains("Firmenname:", text, StringComparison.Ordinal);
        Assert.Contains("Firmenadresse:", text, StringComparison.Ordinal);
        Assert.Contains("UID:", text, StringComparison.Ordinal);
        Assert.Contains("Mitarbeiter:", text, StringComparison.Ordinal);
        Assert.Contains("Schicht-Nr.:", text, StringComparison.Ordinal);
        Assert.Contains("STARTBELEG", text, StringComparison.Ordinal);
        Assert.Contains("Max Mustermann", text, StringComparison.Ordinal);
        Assert.Contains("A1B2C3D4", text, StringComparison.Ordinal);
        Assert.Contains("RKSV / FinanzOnline:", text, StringComparison.Ordinal);
        Assert.Contains("QR_TEST", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EFR", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Stationskennung", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FromClosingReport_MapsMonatsbelegClosingType()
    {
        var template = RksvReportTemplateMapper.FromClosingReport(new PosDailyClosingReportDto
        {
            ClosingType = "Monthly",
            BusinessDate = new DateTime(2026, 7, 1),
            FiscalTotalAmount = 100m,
            FiscalTotalTaxAmount = 20m,
            FiscalTransactionCount = 5,
            CompanyName = "Muster GmbH",
            CompanyAddress = "Wien 1010",
            CompanyVatId = "ATU12345678",
        });

        Assert.Equal(RksvReportNames.Monatsbeleg, template.ReportName);
        Assert.Equal("Muster GmbH", template.CompanyName);
    }

    [Fact]
    public void FromTagesabschluss_IncludesBackdatedOperatorNotice()
    {
        var closingDate = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc);
        var model = new TagesabschlussReportModel
        {
            CashRegisterId = Guid.NewGuid(),
            ClosingDate = closingDate,
            CreatedAt = createdAt,
            IsBackdated = true,
            LateCreationReason = "Technisches Problem / Systemausfall",
            TotalGross = 50m,
            TotalNet = 40m,
        };

        var text = RksvReportTemplateRenderer.Render(
            RksvReportTemplateMapper.FromTagesabschluss(
                model,
                environmentDisplay: "Production",
                rksvFooter: "RKSV-konform",
                qrPayload: "QR",
                registerNumber: "K1"));

        Assert.Contains("verspätet erstellt", text, StringComparison.Ordinal);
        Assert.Contains("Ursprüngliches Datum: 14.07.2026", text, StringComparison.Ordinal);
        Assert.Contains("Erstellt am: 15.07.2026", text, StringComparison.Ordinal);
        Assert.Contains("Grund: Technisches Problem / Systemausfall", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FromReceipt_IncludesLineItems()
    {
        var template = RksvReportTemplateMapper.FromReceipt(new ReceiptDTO
        {
            CashRegisterId = Guid.NewGuid(),
            ReceiptNumber = "R-100",
            GrandTotal = 12m,
            SubTotal = 10m,
            TaxAmount = 2m,
            Items =
            [
                new ReceiptItemDTO
                {
                    Name = "Kaffee",
                    Quantity = 2,
                    UnitPrice = 6m,
                    TotalPrice = 12m,
                },
            ],
            Company = new ReceiptCompanyDTO
            {
                Name = "Cafe",
                Address = "Linz",
                TaxNumber = "ATU11111111",
            },
            RksvFooterLabel = "RKSV-konform",
        });

        var text = RksvReportTemplateRenderer.Render(template);
        Assert.Contains("Positionen:", text, StringComparison.Ordinal);
        Assert.Contains("Kaffee", text, StringComparison.Ordinal);
        Assert.Contains("Beleg-Nr.:       R-100", text, StringComparison.Ordinal);
    }
}
