using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Human-readable backup run size/duration for admin API responses (English labels).</summary>
public static class BackupRunMetricsFormatter
{
    public static int? ComputeDurationSeconds(DateTime? startedAt, DateTime? completedAt)
    {
        if (!startedAt.HasValue || !completedAt.HasValue)
            return null;

        var seconds = (int)Math.Max(0, Math.Round((completedAt.Value - startedAt.Value).TotalSeconds));
        return seconds;
    }

    public static string? FormatDuration(int? durationSeconds)
    {
        if (!durationSeconds.HasValue)
            return null;

        var s = durationSeconds.Value;
        if (s < 60)
            return $"{s}s";

        var m = s / 60;
        var rem = s % 60;
        return rem == 0 ? $"{m}m" : $"{m}m {rem}s";
    }

    public static long? SumArtifactBytes(IEnumerable<BackupArtifact> artifacts)
    {
        var list = artifacts as IReadOnlyCollection<BackupArtifact> ?? artifacts.ToList();
        if (list.Count == 0)
            return null;

        var sum = list.Sum(a => a.ByteSize ?? 0L);
        return sum > 0 ? sum : null;
    }

    public static string? FormatBytes(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value <= 0)
            return null;

        var n = bytes.Value;
        if (n < 1024)
            return $"{n.ToString(CultureInfo.InvariantCulture)} B";

        var kb = n / 1024d;
        if (kb < 1024)
            return $"{kb.ToString("0.0", CultureInfo.InvariantCulture)} KB";

        var mb = kb / 1024d;
        if (mb < 1024)
            return $"{mb.ToString("0.##", CultureInfo.InvariantCulture)} MB";

        var gb = mb / 1024d;
        return $"{gb.ToString("0.##", CultureInfo.InvariantCulture)} GB";
    }

    /// <summary>Logical dump metadata or null when not present.</summary>
    public static bool TryGetOriginalByteSizeFromArtifacts(IEnumerable<BackupArtifact> artifacts, out long originalBytes)
    {
        originalBytes = 0;
        var dump = artifacts.FirstOrDefault(a => a.ArtifactType == BackupArtifactType.LogicalDump);
        if (dump == null || string.IsNullOrWhiteSpace(dump.MetadataJson))
            return false;

        if (!TryReadOriginalByteSize(dump.MetadataJson, out originalBytes) || originalBytes <= 0)
            return false;

        return true;
    }

    /// <summary>Backup artifact total size as a percent of estimated original database size.</summary>
    public static double? ComputeBackupSizePercentOfOriginal(long totalArtifactBytes, long originalBytes)
    {
        if (totalArtifactBytes <= 0 || originalBytes <= 0)
            return null;

        return Math.Round(totalArtifactBytes / (double)originalBytes * 100d, 1);
    }

    /// <summary>
    /// (original / compressed logical dump) * 100 when manifest metadata exposes an original byte size; otherwise null.
    /// </summary>
    public static double? TryComputeCompressionRatio(IEnumerable<BackupArtifact> artifacts)
    {
        var dump = artifacts.FirstOrDefault(a => a.ArtifactType == BackupArtifactType.LogicalDump);
        if (dump?.ByteSize is not > 0)
            return null;

        if (!TryGetOriginalByteSizeFromArtifacts(artifacts, out var original))
            return null;

        return Math.Round(original / (double)dump.ByteSize.Value * 100d, 1);
    }

    private static bool TryReadOriginalByteSize(string metadataJson, out long original)
    {
        original = 0;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;
            if (TryGetPositiveLong(root, "originalByteSize", out original)
                || TryGetPositiveLong(root, "uncompressedByteSize", out original)
                || TryGetPositiveLong(root, "databaseByteSize", out original))
                return true;
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryGetPositiveLong(JsonElement root, string name, out long value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var prop))
            return false;

        switch (prop.ValueKind)
        {
            case JsonValueKind.Number when prop.TryGetInt64(out var n) && n > 0:
                value = n;
                return true;
            default:
                return false;
        }
    }
}
