using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvSpecialReceiptReportServiceTests
{
    [Fact]
    public async Task StartbelegReportService_RendersViaUnifiedTemplate()
    {
        var enricher = new Mock<ITagesabschlussReportEnricher>();
        enricher.Setup(e => e.BuildContextForRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TagesabschlussCloudContext
            {
                CompanyName = "Firma GmbH",
                CompanyAddress = "Graz",
                CompanyVatId = "ATU99999999",
            });

        var svc = new StartbelegReportService(new RksvReportTextService(enricher.Object));
        var text = await svc.GeneratePlainTextReportAsync(new ReceiptDTO
        {
            CashRegisterId = Guid.NewGuid(),
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Startbeleg,
            Company = new ReceiptCompanyDTO { Name = "Firma GmbH", Address = "Graz", TaxNumber = "ATU99999999" },
            RksvFooterLabel = "RKSV-konform",
        });

        Assert.Contains("Firma GmbH", text, StringComparison.Ordinal);
        Assert.Contains("STARTBELEG", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EFR", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NullbelegReportService_RejectsWrongKind()
    {
        var svc = new NullbelegReportService(new RksvReportTextService(Mock.Of<ITagesabschlussReportEnricher>()));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GeneratePlainTextReportAsync(new ReceiptDTO
            {
                RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Startbeleg,
            }));
    }
}
