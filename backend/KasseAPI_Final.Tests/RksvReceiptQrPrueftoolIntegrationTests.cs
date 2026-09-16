using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tests.Fixtures;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// BMF <c>CheckSingleReceipt</c> over the receipt QR wire format. The first test reads the committed
/// fixtures (same contract as <see cref="RksvDepPrueftoolCiSmokeTests"/>); the second generates its own
/// throwaway copy. Neither writes into the committed directory — only
/// <see cref="RksvDepPrueftoolFixtureTests"/> may do that.
/// </summary>
[Collection(PrueftoolFixtureCollection.Name)]
public sealed class RksvReceiptQrPrueftoolIntegrationTests
{
    [SkippableFact]
    public void QrCode_Fixtures_PassBmfCheckSingleReceipt_WhenPrueftoolInstalled()
    {
        Skip.IfNot(
            PrueftoolQrVerificationHelper.IsReceiptVerificationAvailable(out var skipReason),
            skipReason ?? "Prüftool not available.");

        var committed = PrueftoolFixtureLocations.CommittedDirectory;
        var qrRep = Path.Combine(committed, "qr-code-rep.json");
        var crypto = Path.Combine(committed, "crypto-material.json");
        Assert.True(File.Exists(qrRep), $"Missing committed fixture: {qrRep}");
        Assert.True(File.Exists(crypto), $"Missing committed fixture: {crypto}");

        var outputDir = PrueftoolFixtureLocations.CreateTempDirectory("qr");
        try
        {
            var result = PrueftoolQrVerificationHelper.RunCheckSingleReceipt(qrRep, crypto, outputDir);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("PASS", result.VerificationState);
        }
        finally
        {
            PrueftoolFixtureLocations.TryDelete(outputDir);
        }
    }

    [SkippableFact]
    public void QrCode_GeneratedWireFormat_PassesBmfCheckSingleReceipt_WhenPrueftoolInstalled()
    {
        Skip.IfNot(
            PrueftoolQrVerificationHelper.IsReceiptVerificationAvailable(out var skipReason),
            skipReason ?? "Prüftool not available.");

        var generatedDir = PrueftoolFixtureLocations.CreateTempDirectory("qr-generated");
        var outputDir = PrueftoolFixtureLocations.CreateTempDirectory("qr-single");
        try
        {
            var paths = RksvDepPrueftoolFixtureGenerator.Generate(generatedDir);
            var qrCodes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                File.ReadAllText(paths.QrCodeRepPath))!;
            Assert.NotEmpty(qrCodes);

            var singleQrPath = Path.Combine(generatedDir, "single-qr.json");
            var firstQr = qrCodes.First(RksvQrParser.IsStandardRksvV1Format);
            File.WriteAllText(singleQrPath, System.Text.Json.JsonSerializer.Serialize(new[] { firstQr }));

            var result = PrueftoolQrVerificationHelper.RunCheckSingleReceipt(
                singleQrPath,
                paths.CryptoMaterialPath,
                outputDir);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("PASS", result.VerificationState);
        }
        finally
        {
            PrueftoolFixtureLocations.TryDelete(outputDir);
            PrueftoolFixtureLocations.TryDelete(generatedDir);
        }
    }
}
