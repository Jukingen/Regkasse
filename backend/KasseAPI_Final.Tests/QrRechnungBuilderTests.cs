using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class QrRechnungBuilderTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingQrRechnung, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static QrRechnungParty Creditor() => new(
        Name: "CH GmbH",
        AddressLine1: "Bahnhofstrasse 1",
        AddressLine2: null,
        PostalCode: "8001",
        City: "Zürich",
        CountryCode: "CH");

    private static QrRechnungRequest Request(string iban, string currency = "CHF") => new(
        Iban: iban,
        Creditor: Creditor(),
        Debtor: new QrRechnungParty("Kunde AG", "Rue 2", null, "1200", "Genève", "CH"),
        Amount: 108.10m,
        Currency: currency,
        Reference: "RF18539007547034",
        AdditionalInfo: "Rechnung 1");

    [Fact]
    public async Task BuildPayloadAsync_ValidChIban_ReturnsSixShape()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var payload = await builder.BuildPayloadAsync(Request("CH93 0076 2011 6238 5295 7"));

        Assert.Equal("CH9300762011623852957", payload.Iban);
        Assert.Equal("CH GmbH", payload.Creditor.Name);
        Assert.Equal("Bahnhofstrasse 1", payload.Creditor.AddressLine1);
        Assert.Equal("8001", payload.Creditor.PostalCode);
        Assert.Equal("Zürich", payload.Creditor.City);
        Assert.Equal("CH", payload.Creditor.CountryCode);
        Assert.Equal("Kunde AG", payload.Debtor!.Name);
        Assert.Equal(108.10m, payload.Amount);
        Assert.Equal("CHF", payload.Currency);
        Assert.Equal("RF18539007547034", payload.Reference);
        Assert.Equal("Rechnung 1", payload.AdditionalInfo);
    }

    [Fact]
    public async Task BuildPayloadAsync_LiIban_IsAccepted()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var payload = await builder.BuildPayloadAsync(Request("LI21 0881 0000 2324 013A A"));

        Assert.Equal("LI21088100002324013AA", payload.Iban);
    }

    [Fact]
    public async Task BuildPayloadAsync_NonChIban_ThrowsArgumentException()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildPayloadAsync(Request("DE89370400440532013000")));

        Assert.Contains("CH", ex.Message, StringComparison.Ordinal);
        Assert.Contains("LI", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPayloadAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new QrRechnungBuilder(Flags(false));
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildPayloadAsync(Request("CH9300762011623852957")));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingQrRechnung, ex.FeatureName);
    }

    [Fact]
    public async Task BuildPayloadAsync_Scor_EmitsSpcLines()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var payload = await builder.BuildPayloadAsync(Request("CH93 0076 2011 6238 5295 7"));
        var lines = payload.SwissQrText.Split('\n');

        Assert.Equal(QrRechnungReferenceType.Scor, payload.ReferenceType);
        Assert.Equal("SPC", lines[0]);
        Assert.Equal("0200", lines[1]);
        Assert.Equal("1", lines[2]);
        Assert.Equal("CH9300762011623852957", lines[3]);
        Assert.Equal("S", lines[4]);
        Assert.Equal("SCOR", lines[^4]);
        Assert.Equal("RF18539007547034", lines[^3]);
        Assert.Equal("Rechnung 1", lines[^2]);
        Assert.Equal("EPD", lines[^1]);
        Assert.DoesNotContain("\nK\n", payload.SwissQrText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPayloadAsync_Qrr_RequiresQrIban()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var payload = await builder.BuildPayloadAsync(new QrRechnungRequest(
            Iban: "CH44 3199 9123 0008 8901 2",
            Creditor: Creditor(),
            Debtor: null,
            Amount: 108.50m,
            Currency: "CHF",
            Reference: "210000000003139471430009017",
            AdditionalInfo: null,
            ReferenceType: QrRechnungReferenceType.Qrr));

        Assert.Equal(QrRechnungReferenceType.Qrr, payload.ReferenceType);
        Assert.Contains("\nQRR\n210000000003139471430009017\n", payload.SwissQrText, StringComparison.Ordinal);
        Assert.Contains("\n108.50\nCHF\n", payload.SwissQrText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPayloadAsync_Non_RejectsStructuredReference()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildPayloadAsync(Request("CH9300762011623852957") with
            {
                Reference = "RF18539007547034",
                ReferenceType = QrRechnungReferenceType.Non,
            }));

        Assert.Contains("NON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPayloadAsync_EurQrr_Throws()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildPayloadAsync(new QrRechnungRequest(
                "CH4431999123000889012",
                Creditor(),
                null,
                10m,
                "EUR",
                "210000000003139471430009017",
                null,
                QrRechnungReferenceType.Qrr)));

        Assert.Contains("EUR", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPdfAsync_ChInvoice_SpcVersionIbanAndReferenceType()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var request = Request("CH93 0076 2011 6238 5295 7");
        var pdf = await builder.BuildPdfAsync(request);
        var payload = await builder.BuildPayloadAsync(request);

        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF"u8.ToArray(), pdf.Take(4).ToArray());
        Assert.StartsWith("SPC\n", payload.SwissQrText, StringComparison.Ordinal);
        Assert.StartsWith("SPC\n0200\n", payload.SwissQrText, StringComparison.Ordinal);
        Assert.Equal("0200", payload.SwissQrText.Split('\n')[1]);
        Assert.True(SwissQrEncoder.Mod97IsValid(payload.Iban));
        Assert.StartsWith("CH", payload.Iban, StringComparison.Ordinal);
        Assert.Contains(
            SwissQrEncoder.ToWire(payload.ReferenceType),
            new[] { "QRR", "SCOR", "NON" },
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task BuildPdfAsync_FlagOn_WritesPdfWithSwissCross()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var pdf = await builder.BuildPdfAsync(Request("CH9300762011623852957"));

        Assert.True(pdf.Length > 500);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);

        var payload = await builder.BuildPayloadAsync(Request("CH9300762011623852957"));
        var modules = QrRechnungPdf.SwissCrossMatrix(payload.SwissQrText);
        var size = modules.GetLength(0);
        var mid = size / 2;
        Assert.False(modules[mid, mid]);
        Assert.True(modules[mid - 3, mid - 3]);
    }

    [Fact]
    public async Task BuildPdfAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new QrRechnungBuilder(Flags(false));
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildPdfAsync(Request("CH9300762011623852957")));

        Assert.Equal(FeatureFlagNames.EInvoicingQrRechnung, ex.FeatureName);
    }

    [Fact]
    public async Task BuildPdfAsync_AuditsRelativePath_WithoutIban()
    {
        const string iban = "CH9300762011623852957";
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        const string relativePath = "qr-rechnung/bill.pdf";
        var calls = new List<(AuditEventType? Type, string? CorrelationId, Guid? TenantId, object? NewValues)>();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback(new InvocationAction(invocation =>
            {
                calls.Add((
                    invocation.Arguments[12] as AuditEventType?,
                    invocation.Arguments[10] as string,
                    invocation.Arguments[14] as Guid?,
                    invocation.Arguments[16]));
            }))
            .ReturnsAsync(new AuditLog());

        var builder = new QrRechnungBuilder(Flags(true), audit.Object);
        var request = Request(iban) with
        {
            InvoiceId = invoiceId,
            TenantId = tenantId,
            PdfPathRelative = relativePath,
        };

        var pdf = await builder.BuildPdfAsync(request);

        Assert.NotEmpty(pdf);
        var pdfCall = Assert.Single(calls, call => call.Type == AuditEventType.QrRechnungPdfGenerated);
        Assert.Equal(tenantId, pdfCall.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(pdfCall.CorrelationId));
        var json = JsonSerializer.Serialize(pdfCall.NewValues);
        Assert.Contains(relativePath, json, StringComparison.Ordinal);
        Assert.Contains(invoiceId.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(iban, json, StringComparison.Ordinal);
        Assert.DoesNotContain(":\\", json, StringComparison.Ordinal);
        Assert.Contains(calls, call => call.Type == AuditEventType.QrRechnungPayloadBuilt);
    }

    [Fact]
    public async Task BuildPdfAsync_AbsolutePath_DoesNotRender()
    {
        var audit = new Mock<IAuditLogService>();
        var builder = new QrRechnungBuilder(Flags(true), audit.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildPdfAsync(Request("CH9300762011623852957") with
            {
                PdfPathRelative = "C:\\bills\\bill.pdf",
            }));

        audit.Verify(
            a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()),
            Times.Never);
    }
}
