namespace KasseAPI_Final.Services.Backup.PgDump;

/// <summary>
/// pg_dump -Fc çıktısı için hafif dosya doğrulaması (restore kanıtı değil; sahte başarıları azaltır).
/// </summary>
public static class PgDumpCustomFormatSanity
{
    /// <summary>PostgreSQL custom archive imzası (pg_backup_custom.c).</summary>
    public static ReadOnlySpan<byte> Magic => "PGDMP"u8;

    /// <summary>
    /// Dosya en az <see cref="Magic"/> uzunluğunda olmalı ve bu baytlarla başlamalıdır.
    /// </summary>
    public static bool TryValidate(string absolutePath, out string? failureReason)
    {
        failureReason = null;
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            failureReason = "Path is empty.";
            return false;
        }

        try
        {
            using var fs = new FileStream(
                absolutePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16,
                FileOptions.SequentialScan);

            Span<byte> header = stackalloc byte[Magic.Length];
            var read = fs.Read(header);
            if (read < Magic.Length)
            {
                failureReason = "Dump file is too short for a PostgreSQL custom-format header.";
                return false;
            }

            if (!header.SequenceEqual(Magic))
            {
                failureReason = "Dump file does not start with PGDMP (expected pg_dump -Fc output).";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            return false;
        }
    }
}
