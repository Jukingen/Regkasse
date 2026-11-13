using System.IO.Compression;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Content-aware GZip helpers and ZIP entry level selection for backup packages.
/// Does not replace <c>pg_dump -Z</c> (custom-format zlib) — that stays on <see cref="Configuration.BackupOptions.PgDumpCompressionLevel"/>.
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// Compress <paramref name="data"/> with GZip.
    /// When <paramref name="level"/> is null, picks Optimal for text-like payloads and Fastest for binary.
    /// </summary>
    Task<byte[]> CompressAsync(
        byte[] data,
        CompressionLevel? level = null,
        CancellationToken ct = default);

    /// <summary>Decompress a GZip payload produced by <see cref="CompressAsync"/>.</summary>
    Task<byte[]> DecompressAsync(byte[] compressed, CancellationToken ct = default);

    /// <summary>Heuristic: printable UTF-8/ASCII-heavy samples → text.</summary>
    bool IsTextData(ReadOnlySpan<byte> data);

    /// <summary>Resolve GZip level from payload bytes (null → auto).</summary>
    CompressionLevel ResolveLevel(ReadOnlySpan<byte> data, CompressionLevel? overrideLevel = null);

    /// <summary>
    /// ZIP entry level by name: JSON/text → Optimal; already-compressed (.zip/.gz/.dump) → NoCompression/Fastest.
    /// </summary>
    CompressionLevel ResolveZipEntryLevel(string entryName);
}
