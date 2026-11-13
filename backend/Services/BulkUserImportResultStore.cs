using System.Collections.Concurrent;
using System.Text;

namespace KasseAPI_Final.Services;

public interface IBulkUserImportResultStore
{
    Task<string> SaveResultCsvAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default);
    Task<Stream?> OpenResultAsync(string resultId, CancellationToken cancellationToken = default);
}

/// <summary>Stores bulk-import result CSV files on disk with short TTL.</summary>
public sealed class BulkUserImportResultStore : IBulkUserImportResultStore
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    private readonly string _directory;
    private readonly ConcurrentDictionary<string, DateTime> _createdUtc = new();

    public BulkUserImportResultStore(IWebHostEnvironment environment)
    {
        _directory = Path.Combine(environment.ContentRootPath, "data", "bulk-user-import-results");
        Directory.CreateDirectory(_directory);
    }

    public async Task<string> SaveResultCsvAsync(IEnumerable<string> lines, CancellationToken cancellationToken = default)
    {
        PurgeExpired();
        var id = Guid.NewGuid().ToString("N");
        var path = Path.Combine(_directory, $"{id}.csv");
        var content = string.Join(Environment.NewLine, lines);
        await File.WriteAllTextAsync(path, "\uFEFF" + content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        _createdUtc[id] = DateTime.UtcNow;
        return id;
    }

    public Task<Stream?> OpenResultAsync(string resultId, CancellationToken cancellationToken = default)
    {
        PurgeExpired();
        if (string.IsNullOrWhiteSpace(resultId) || resultId.Contains("..", StringComparison.Ordinal))
            return Task.FromResult<Stream?>(null);

        var safeId = Path.GetFileNameWithoutExtension(resultId.Trim());
        var path = Path.Combine(_directory, $"{safeId}.csv");
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = File.OpenRead(path);
        return Task.FromResult<Stream?>(stream);
    }

    private void PurgeExpired()
    {
        var cutoff = DateTime.UtcNow - MaxAge;
        foreach (var entry in _createdUtc.ToArray())
        {
            if (entry.Value >= cutoff)
                continue;

            _createdUtc.TryRemove(entry.Key, out _);
            var path = Path.Combine(_directory, $"{entry.Key}.csv");
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
