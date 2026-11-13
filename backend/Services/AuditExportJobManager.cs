using System.Collections.Concurrent;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public enum AuditExportJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
}

public sealed class AuditExportJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public AuditExportJobStatus Status { get; set; }
    public int? MatchedRows { get; set; }
    public string? Message { get; set; }
    public string? DownloadFileName { get; set; }
}

public interface IAuditExportJobManager
{
    Task<string> StartJobAsync(Guid tenantId, AuditLogQueryFilters filters, string format, CancellationToken requestAborted = default);

    AuditExportJobStatusDto? GetStatus(string jobId);

    bool TryOpenDownload(string jobId, out Stream? stream, out string? fileName, out string? contentType);
}

internal sealed class AuditExportJobEntry
{
    public string JobId { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public AuditLogQueryFilters Filters { get; init; } = new();
    public string Format { get; init; } = "csv";
    public AuditExportJobStatus Status { get; set; } = AuditExportJobStatus.Queued;
    public int? MatchedRows { get; set; }
    public string? Message { get; set; }
    public string? FilePath { get; set; }
    public string? DownloadFileName { get; set; }
    public string ContentType { get; set; } = "text/csv";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
}

public sealed class AuditExportJobManager : IAuditExportJobManager
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, AuditExportJobEntry> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditExportJobManager> _logger;
    private readonly string _storageRoot;

    public AuditExportJobManager(IServiceScopeFactory scopeFactory, ILogger<AuditExportJobManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _storageRoot = Path.Combine(Path.GetTempPath(), "regkasse-audit-exports");
        Directory.CreateDirectory(_storageRoot);
    }

    public Task<string> StartJobAsync(
        Guid tenantId,
        AuditLogQueryFilters filters,
        string format,
        CancellationToken requestAborted = default)
    {
        PurgeStaleJobs();
        var jobId = Guid.NewGuid().ToString("N");
        var entry = new AuditExportJobEntry
        {
            JobId = jobId,
            TenantId = tenantId,
            Filters = filters,
            Format = format,
        };
        _jobs[jobId] = entry;

        _ = Task.Run(async () =>
        {
            try
            {
                entry.Status = AuditExportJobStatus.Running;
                using var scope = _scopeFactory.CreateScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                tenantAccessor.TenantId = tenantId;

                var export = scope.ServiceProvider.GetRequiredService<IAuditExportService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var fileNaming = scope.ServiceProvider.GetRequiredService<IFileNamingService>();
                var count = await export.CountForExportAsync(entry.Filters, CancellationToken.None).ConfigureAwait(false);
                entry.MatchedRows = count;

                var tenantSlug = await db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Slug)
                    .FirstOrDefaultAsync(CancellationToken.None)
                    .ConfigureAwait(false);

                var ext = AuditExportFileNames.NormalizeExtension(format);
                entry.DownloadFileName = fileNaming.GenerateFileName(
                    AuditExportFileNames.Prefix,
                    ext,
                    registerId: ExportFileNameSegments.DateOnly(entry.Filters.StartDate),
                    additional: ExportFileNameSegments.DateOnly(entry.Filters.EndDate),
                    tenantSlug: tenantSlug);
                entry.ContentType = AuditExportFileNames.ContentTypeForFormat(format);
                var path = Path.Combine(_storageRoot, $"{jobId}.{ext}");
                await using (var fs = File.Create(path))
                {
                    await export.StreamExportAsync(entry.Filters, entry.Format, fs, CancellationToken.None).ConfigureAwait(false);
                }

                entry.FilePath = path;
                entry.Status = AuditExportJobStatus.Completed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit export job {JobId} failed", jobId);
                entry.Status = AuditExportJobStatus.Failed;
                entry.Message = ex.Message;
            }
        }, CancellationToken.None);

        return Task.FromResult(jobId);
    }

    public AuditExportJobStatusDto? GetStatus(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return null;

        return new AuditExportJobStatusDto
        {
            JobId = entry.JobId,
            Status = entry.Status,
            MatchedRows = entry.MatchedRows,
            Message = entry.Message,
            DownloadFileName = entry.DownloadFileName,
        };
    }

    public bool TryOpenDownload(string jobId, out Stream? stream, out string? fileName, out string? contentType)
    {
        stream = null;
        fileName = null;
        contentType = null;
        if (!_jobs.TryGetValue(jobId, out var entry) || entry.Status != AuditExportJobStatus.Completed || string.IsNullOrEmpty(entry.FilePath) || !File.Exists(entry.FilePath))
            return false;

        stream = File.OpenRead(entry.FilePath);
        fileName = entry.DownloadFileName
            ?? new FileNamingService(NullCurrentTenantAccessor.Instance).GenerateFileName(
                AuditExportFileNames.Prefix,
                "csv",
                registerId: "all",
                additional: "all");
        contentType = entry.ContentType;
        return true;
    }

    private void PurgeStaleJobs()
    {
        var cutoff = DateTime.UtcNow - JobTtl;
        foreach (var kv in _jobs.ToArray())
        {
            if (kv.Value.CreatedUtc < cutoff)
            {
                _jobs.TryRemove(kv.Key, out var removed);
                TryDeleteFile(removed?.FilePath);
            }
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        { File.Delete(path); }
        catch { /* best effort */ }
    }
}
