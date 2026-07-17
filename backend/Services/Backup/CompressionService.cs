using System.IO.Compression;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Backup compression helpers: content-aware GZip + ZIP entry level hints.
/// Sketch note: caller-supplied level is optional; auto mode uses text→Optimal, binary→Fastest.
/// </summary>
public sealed class CompressionService : ICompressionService
{
    public static CompressionService Shared { get; } = new();

    private const int SampleSize = 512;

    public async Task<byte[]> CompressAsync(
        byte[] data,
        CompressionLevel? level = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var effective = ResolveLevel(data, level);

        using var output = new MemoryStream(capacity: Math.Max(64, data.Length / 2));
        await using (var gzip = new GZipStream(output, effective, leaveOpen: true))
        {
            await gzip.WriteAsync(data.AsMemory(), ct);
        }

        return output.ToArray();
    }

    public async Task<byte[]> DecompressAsync(byte[] compressed, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(compressed);

        using var input = new MemoryStream(compressed, writable: false);
        await using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output, ct);
        return output.ToArray();
    }

    public bool IsTextData(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return true;

        var sample = data.Length <= SampleSize ? data : data[..SampleSize];

        // NUL bytes strongly indicate binary (images, dumps, nested archives).
        if (sample.IndexOf((byte)0) >= 0)
            return false;

        var printable = 0;
        foreach (var b in sample)
        {
            if (b is >= 32 and <= 126 or 9 or 10 or 13)
                printable++;
            else if (b >= 128)
                printable++; // allow UTF-8 high bytes as text-ish
        }

        return printable >= sample.Length * 0.85;
    }

    public CompressionLevel ResolveLevel(ReadOnlySpan<byte> data, CompressionLevel? overrideLevel = null)
    {
        if (overrideLevel.HasValue)
            return overrideLevel.Value;

        return IsTextData(data) ? CompressionLevel.Optimal : CompressionLevel.Fastest;
    }

    public CompressionLevel ResolveZipEntryLevel(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            return CompressionLevel.Optimal;

        var name = entryName.Trim();
        var ext = Path.GetExtension(name);
        if (string.IsNullOrEmpty(ext) && name.Contains('.', StringComparison.Ordinal))
        {
            // Nested paths like tenants/foo.tenant.zip
            ext = Path.GetExtension(name.Replace('\\', '/').Split('/').Last());
        }

        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            return CompressionLevel.Optimal;

        // Already compressed / custom binary — avoid double-compress CPU cost.
        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gz", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".dump", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".7z", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tenant.zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".system.zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".tenant.incr.zip", StringComparison.OrdinalIgnoreCase))
            return CompressionLevel.NoCompression;

        return CompressionLevel.Optimal;
    }
}
