using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TagesabschlussCloudReportTemplateTests
{
    [Fact]
    public void Render_IncludesCloudSections_AndOmitsOnPremiseFields()
    {
        var registerId = Guid.Parse("11111111222222223333333344444444");
        var report = TagesabschlussCloudReportTemplate.Render(new TagesabschlussCloudReportTemplate.RenderInput(
            new TagesabschlussReportModel
            {
                CashRegisterId = registerId,
                ClosingDate = new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc),
                CompanyName = "Muster GmbH",
                CompanyAddress = "Wien 1010",
                CompanyVatId = "ATU12345678",
                TotalGross = 100m,
                TotalNet = 83.33m,
                TaxRate20 = 16.67m,
                CashTotal = 60m,
                CardTotal = 40m,
                TransactionCount = 3,
                TseProviderLabel = "fiskaly Cloud-HSM",
                DepExportStatusLabel = "Exportiert",
                TseSignature = "sig.value",
                TseSignatureVerified = true,
                HasStartbeleg = true,
                CashierName = "Anna",
                ShiftNumber = "DEADBEEF",
            },
            "🚀 PRODUCTION",
            "RKSV footer",
            "QR_PAYLOAD",
            "KASSE-01"));

        Assert.Contains("Firmenname:", report, StringComparison.Ordinal);
        Assert.Contains("Muster GmbH", report, StringComparison.Ordinal);
        Assert.Contains("Mitarbeiter:", report, StringComparison.Ordinal);
        Assert.Contains("Anna", report, StringComparison.Ordinal);
        Assert.Contains("Schicht-Nr.:", report, StringComparison.Ordinal);
        Assert.Contains("Kassen-ID:       11111111222222223333333344444444", report, StringComparison.Ordinal);
        Assert.Contains("Kasse:           KASSE-01", report, StringComparison.Ordinal);
        Assert.Contains("fiskaly Cloud-HSM", report, StringComparison.Ordinal);
        Assert.Contains("Finanzübersicht:", report, StringComparison.Ordinal);
        Assert.Contains("RKSV / FinanzOnline:", report, StringComparison.Ordinal);
        Assert.Contains("Verifizierung:   GEPRÜFT", report, StringComparison.Ordinal);
        Assert.Contains("QR_PAYLOAD", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Signaturkette", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Stationskennung", report, StringComparison.Ordinal);
    }
}
