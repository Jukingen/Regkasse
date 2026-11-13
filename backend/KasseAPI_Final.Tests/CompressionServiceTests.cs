using System.IO.Compression;
using System.Text;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CompressionServiceTests
{
    private readonly CompressionService _sut = new();

    [Fact]
    public async Task CompressAsync_roundtrips_text_with_optimal()
    {
        var original = Encoding.UTF8.GetBytes("""{"hello":"world","n":123}""" + new string('a', 2000));
        var compressed = await _sut.CompressAsync(original);
        Assert.True(compressed.Length < original.Length);

        var roundtrip = await _sut.DecompressAsync(compressed);
        Assert.Equal(original, roundtrip);
    }

    [Fact]
    public void ResolveLevel_text_is_optimal_binary_is_fastest()
    {
        var text = Encoding.UTF8.GetBytes("{\"a\":1}");
        var binary = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0x00, 0x10 };

        Assert.Equal(CompressionLevel.Optimal, _sut.ResolveLevel(text));
        Assert.Equal(CompressionLevel.Fastest, _sut.ResolveLevel(binary));
        Assert.True(_sut.IsTextData(text));
        Assert.False(_sut.IsTextData(binary));
    }

    [Fact]
    public void ResolveLevel_respects_explicit_override()
    {
        var text = Encoding.UTF8.GetBytes("plain text");
        Assert.Equal(
            CompressionLevel.NoCompression,
            _sut.ResolveLevel(text, CompressionLevel.NoCompression));
    }

    [Theory]
    [InlineData("products.json", CompressionLevel.Optimal)]
    [InlineData("identity/users.json", CompressionLevel.Optimal)]
    [InlineData("tenants/acme.tenant.zip", CompressionLevel.NoCompression)]
    [InlineData("backup.dump", CompressionLevel.NoCompression)]
    [InlineData("archive.gz", CompressionLevel.NoCompression)]
    public void ResolveZipEntryLevel_by_extension(string entry, CompressionLevel expected)
    {
        Assert.Equal(expected, _sut.ResolveZipEntryLevel(entry));
    }

    [Fact]
    public async Task CompressAsync_explicit_level_is_used()
    {
        var data = Encoding.UTF8.GetBytes(new string('x', 4000));
        var a = await _sut.CompressAsync(data, CompressionLevel.NoCompression);
        var b = await _sut.CompressAsync(data, CompressionLevel.Optimal);
        // NoCompression GZip still has headers but should not shrink as much as Optimal.
        Assert.True(b.Length <= a.Length);
    }
}
