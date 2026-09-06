using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FilesystemWormCloudStorageProviderTests
{
    [Fact]
    public async Task Upload_then_overwrite_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "regkasse-worm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sut = Create(root);
            var first = await sut.UploadAsync(new CloudStorageUploadRequest
            {
                ObjectKey = "run1/a.bin",
                Content = "hello"u8.ToArray()
            });
            Assert.True(first.Success);
            Assert.True(await sut.ExistsAsync("run1/a.bin"));

            var second = await sut.UploadAsync(new CloudStorageUploadRequest
            {
                ObjectKey = "run1/a.bin",
                Content = "world"u8.ToArray()
            });
            Assert.False(second.Success);
            Assert.Contains("WORM", second.Error, StringComparison.Ordinal);

            await using var stream = await sut.DownloadAsync("run1/a.bin");
            using var reader = new StreamReader(stream);
            Assert.Equal("hello", await reader.ReadToEndAsync());
        }
        finally
        {
            try
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
    }

    private static FilesystemWormCloudStorageProvider Create(string root)
    {
        var monitor = new Mock<IOptionsMonitor<BackupOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(new BackupOptions { ExternalArchiveRoot = root });
        return new FilesystemWormCloudStorageProvider(monitor.Object, NullLogger<FilesystemWormCloudStorageProvider>.Instance);
    }
}
