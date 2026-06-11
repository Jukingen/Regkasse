using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tests.Fixtures;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvReceiptQrPrueftoolIntegrationTests
{
    private static string FixtureDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tests", "fixtures", "prueftool"));

    [SkippableFact]
    public void QrCode_Fixtures_PassBmfCheckSingleReceipt_WhenPrueftoolInstalled()
    {
        Skip.IfNot(
            PrueftoolQrVerificationHelper.IsReceiptVerificationAvailable(out var skipReason),
            skipReason ?? "Prüftool not available.");

        RksvDepPrueftoolFixtureGenerator.Generate(FixtureDirectory);

        var qrRep = Path.Combine(FixtureDirectory, "qr-code-rep.json");
        var crypto = Path.Combine(FixtureDirectory, "crypto-material.json");
        var outputDir = Path.Combine(Path.GetTempPath(), "regkasse-prueftool-qr", Guid.NewGuid().ToString("N"));

        var result = PrueftoolQrVerificationHelper.RunCheckSingleReceipt(qrRep, crypto, outputDir);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PASS", result.VerificationState);
    }

    [SkippableFact]
    public void QrCode_GeneratedWireFormat_PassesBmfCheckSingleReceipt_WhenPrueftoolInstalled()
    {
        Skip.IfNot(
            PrueftoolQrVerificationHelper.IsReceiptVerificationAvailable(out var skipReason),
            skipReason ?? "Prüftool not available.");

        var paths = RksvDepPrueftoolFixtureGenerator.Generate(FixtureDirectory);
        var qrCodes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(paths.QrCodeRepPath))!;
        Assert.NotEmpty(qrCodes);

        var singleQrPath = Path.Combine(Path.GetTempPath(), $"regkasse-single-qr-{Guid.NewGuid():N}.json");
        var firstQr = qrCodes.First(q =>
            RksvQrParser.IsStandardRksvV1Format(q));
        File.WriteAllText(singleQrPath, System.Text.Json.JsonSerializer.Serialize(new[] { firstQr }));

        var outputDir = Path.Combine(Path.GetTempPath(), "regkasse-prueftool-qr-single", Guid.NewGuid().ToString("N"));
        var result = PrueftoolQrVerificationHelper.RunCheckSingleReceipt(
            singleQrPath,
            paths.CryptoMaterialPath,
            outputDir);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PASS", result.VerificationState);
    }
}
