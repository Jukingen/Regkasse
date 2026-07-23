using KasseAPI_Final.Services.Rksv;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvDepExportFileNamesTests
{
    [Fact]
    public void Build_UsesCanonicalPattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var name = RksvDepExportFileNames.Build("cafe", "k1", at);
        Assert.Equal("dep-export_cafe_k1_20260722_143022.json", name);
    }

    [Fact]
    public void Build_SanitizesUnsafeCharacters()
    {
        var at = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var name = RksvDepExportFileNames.Build("cafe/beispiel", "KASSE 001", at);
        Assert.Equal("dep-export_cafe_beispiel_KASSE_001_20260102_030405.json", name);
    }

    [Theory]
    [InlineData(null, "tenant")]
    [InlineData("", "tenant")]
    [InlineData("   ", "tenant")]
    [InlineData("***", "tenant")]
    public void SanitizeSegment_FallsBackWhenEmpty(string? value, string fallback)
    {
        Assert.Equal(fallback, RksvDepExportFileNames.SanitizeSegment(value, fallback));
    }
}
