using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupEncryptionServiceTests
{
    private static IOptionsMonitor<BackupOptions> Monitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static string KeyBase64() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Encrypt_Decrypt_round_trip()
    {
        var key = KeyBase64();
        var sut = new BackupEncryptionService(
            Monitor(new BackupOptions { EncryptionEnabled = true, EncryptionKeyBase64 = key }),
            NullLogger<BackupEncryptionService>.Instance);

        var plain = Encoding.UTF8.GetBytes("tenant fiscal dump payload");
        var cipher = sut.Encrypt(plain);
        Assert.True(sut.LooksEncrypted(cipher));
        Assert.False(plain.AsSpan().SequenceEqual(cipher));

        var roundTrip = sut.Decrypt(cipher);
        Assert.Equal(plain, roundTrip);
    }

    [Fact]
    public void Decrypt_passthrough_when_not_encrypted()
    {
        var sut = new BackupEncryptionService(
            Monitor(new BackupOptions { EncryptionEnabled = true, EncryptionKeyBase64 = KeyBase64() }),
            NullLogger<BackupEncryptionService>.Instance);

        var plain = Encoding.UTF8.GetBytes("PGDMP plain dump");
        Assert.Equal(plain, sut.Decrypt(plain));
    }

    [Fact]
    public async Task EncryptFileInPlace_is_noop_when_disabled()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"bak_enc_{Guid.NewGuid():N}.bin");
        try
        {
            var payload = Encoding.UTF8.GetBytes("plain-bytes");
            await File.WriteAllBytesAsync(temp, payload);

            var sut = new BackupEncryptionService(
                Monitor(new BackupOptions { EncryptionEnabled = false, EncryptionKeyBase64 = KeyBase64() }),
                NullLogger<BackupEncryptionService>.Instance);

            await sut.EncryptFileInPlaceAsync(temp);
            Assert.Equal(payload, await File.ReadAllBytesAsync(temp));
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    [Fact]
    public async Task EncryptFileInPlace_then_DecryptFileTo_round_trip()
    {
        var src = Path.Combine(Path.GetTempPath(), $"bak_src_{Guid.NewGuid():N}.bin");
        var dest = Path.Combine(Path.GetTempPath(), $"bak_dst_{Guid.NewGuid():N}.bin");
        try
        {
            var payload = Encoding.UTF8.GetBytes("sensitive-backup-bytes");
            await File.WriteAllBytesAsync(src, payload);

            var sut = new BackupEncryptionService(
                Monitor(new BackupOptions { EncryptionEnabled = true, EncryptionKeyBase64 = KeyBase64() }),
                NullLogger<BackupEncryptionService>.Instance);

            await sut.EncryptFileInPlaceAsync(src);
            Assert.True(sut.LooksEncrypted(await File.ReadAllBytesAsync(src)));

            await sut.DecryptFileToAsync(src, dest);
            Assert.Equal(payload, await File.ReadAllBytesAsync(dest));
        }
        finally
        {
            if (File.Exists(src))
                File.Delete(src);
            if (File.Exists(dest))
                File.Delete(dest);
        }
    }
}
