using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseCertificatePdfGeneratorTests
{
    [Fact]
    public void Generate_ReturnsPdfBytes()
    {
        var pdf = LicenseCertificatePdfGenerator.Generate(
            new LicenseCertificatePdfModel(
                "Cafe Muster",
                "cafe",
                "REGK-20270101-…D9",
                "active",
                new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc)));

        Assert.True(pdf.Length > 4);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }
}
