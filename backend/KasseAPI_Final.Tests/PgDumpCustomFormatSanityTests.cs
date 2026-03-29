using KasseAPI_Final.Services.Backup.PgDump;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PgDumpCustomFormatSanityTests
{
    [Fact]
    public void TryValidate_accepts_file_starting_with_PG_custom_magic()
    {
        var path = Path.Combine(Path.GetTempPath(), "pgdump_magic_ok_" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, [(byte)'P', (byte)'G', (byte)'D', (byte)'M', (byte)'P', 9]);

            Assert.True(PgDumpCustomFormatSanity.TryValidate(path, out var reason));
            Assert.Null(reason);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void TryValidate_rejects_wrong_magic()
    {
        var path = Path.Combine(Path.GetTempPath(), "pgdump_magic_bad_" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "hello");

            Assert.False(PgDumpCustomFormatSanity.TryValidate(path, out var reason));
            Assert.NotNull(reason);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }
}
