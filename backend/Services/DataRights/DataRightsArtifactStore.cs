using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.DataRights;

/// <summary>Filesystem store for GDPR export ZIP artifacts under App_Data/data-exports.</summary>
public interface IDataRightsArtifactStore
{
    string GetAbsolutePath(string relativePath);

    Task<string> SaveExportAsync(Guid tenantId, Guid requestId, byte[] data, CancellationToken ct = default);

    Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default);

    void TryDelete(string? relativePath);
}

public sealed class DataRightsArtifactStore : IDataRightsArtifactStore
{
    private readonly string _root;

    public DataRightsArtifactStore(IHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "App_Data", "data-exports");
    }

    public string GetAbsolutePath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid artifact path.");
        }

        return full;
    }

    public async Task<string> SaveExportAsync(
        Guid tenantId,
        Guid requestId,
        byte[] data,
        CancellationToken ct = default)
    {
        var relative = Path.Combine(tenantId.ToString("N"), $"{requestId:N}.zip");
        var absolute = GetAbsolutePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, data, ct).ConfigureAwait(false);
        return relative.Replace('\\', '/');
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var absolute = GetAbsolutePath(relativePath);
        if (!File.Exists(absolute))
            return null;
        return await File.ReadAllBytesAsync(absolute, ct).ConfigureAwait(false);
    }

    public void TryDelete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return;
        try
        {
            var absolute = GetAbsolutePath(relativePath);
            if (File.Exists(absolute))
                File.Delete(absolute);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
