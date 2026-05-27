using System.Collections.Concurrent;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public sealed record BulkImportActorContext(
    string ActorUserId,
    string ActorRole,
    bool ActorIsSuperAdmin,
    Guid? ActorTenantId);

public interface IBulkUserImportJobManager
{
    Task<BulkImportStartResponseDto> StartJobAsync(
        List<BulkImportRow> rows,
        BulkImportActorContext actor,
        CancellationToken requestAborted = default);

    BulkImportJobStatusDto? GetStatus(string jobId);

    bool TryCancel(string jobId);
}

public sealed class BulkImportJobEntry
{
    private readonly object _lock = new();
    private readonly List<BulkImportErrorDto> _errors = new();

    public string JobId { get; init; } = string.Empty;
    public BulkImportJobStatus Status { get; set; } = BulkImportJobStatus.Queued;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Message { get; set; }
    public CancellationTokenSource Cancellation { get; init; } = new();
    public BulkImportActorContext Actor { get; init; } = null!;
    public List<BulkImportRow> Rows { get; init; } = new();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    public void AddError(BulkImportErrorDto error)
    {
        lock (_lock)
        {
            _errors.Add(error);
        }
    }

    public IReadOnlyList<BulkImportErrorDto> SnapshotErrors(int max = 100)
    {
        lock (_lock)
        {
            if (_errors.Count <= max)
                return _errors.ToList();
            return _errors.Skip(_errors.Count - max).ToList();
        }
    }

    public List<BulkImportErrorDto> AllErrors()
    {
        lock (_lock)
        {
            return _errors.ToList();
        }
    }
}

/// <summary>In-memory bulk import jobs (survives HTTP disconnect; poll for progress).</summary>
public sealed class BulkUserImportJobManager : IBulkUserImportJobManager
{
    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(2);
    private readonly ConcurrentDictionary<string, BulkImportJobEntry> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkUserImportJobManager> _logger;

    public BulkUserImportJobManager(
        IServiceScopeFactory scopeFactory,
        ILogger<BulkUserImportJobManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task<BulkImportStartResponseDto> StartJobAsync(
        List<BulkImportRow> rows,
        BulkImportActorContext actor,
        CancellationToken requestAborted = default)
    {
        PurgeStaleJobs();
        var jobId = Guid.NewGuid().ToString("N");
        var entry = new BulkImportJobEntry
        {
            JobId = jobId,
            TotalRows = rows.Count,
            Actor = actor,
            Rows = rows,
            Status = BulkImportJobStatus.Queued,
        };
        _jobs[jobId] = entry;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<BulkUserImportService>();
                await processor.RunJobAsync(entry, requestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk import job {JobId} crashed", jobId);
                entry.Status = BulkImportJobStatus.Failed;
                entry.Message = "Import job failed unexpectedly.";
            }
        }, CancellationToken.None);

        return Task.FromResult(new BulkImportStartResponseDto
        {
            JobId = jobId,
            TotalRows = rows.Count,
        });
    }

    public BulkImportJobStatusDto? GetStatus(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return null;

        var dto = new BulkImportJobStatusDto
        {
            JobId = entry.JobId,
            Status = entry.Status,
            TotalRows = entry.TotalRows,
            ProcessedRows = entry.ProcessedRows,
            SuccessCount = entry.SuccessCount,
            FailedCount = entry.FailedCount,
            Errors = entry.SnapshotErrors(),
            Message = entry.Message,
        };

        if (entry.Status is BulkImportJobStatus.Completed or BulkImportJobStatus.Cancelled)
        {
            dto.Result = new BulkImportResultDto
            {
                TotalRows = entry.TotalRows,
                SuccessCount = entry.SuccessCount,
                FailedCount = entry.FailedCount,
                Errors = entry.AllErrors(),
                DownloadUrl = entry.DownloadUrl,
            };
        }

        return dto;
    }

    public bool TryCancel(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return false;

        if (entry.Status is BulkImportJobStatus.Completed or BulkImportJobStatus.Cancelled or BulkImportJobStatus.Failed)
            return false;

        entry.Cancellation.Cancel();
        entry.Status = BulkImportJobStatus.Cancelled;
        entry.Message = "Import cancelled by user.";
        return true;
    }

    private void PurgeStaleJobs()
    {
        var cutoff = DateTime.UtcNow - JobTtl;
        foreach (var key in _jobs.Keys)
        {
            if (_jobs.TryGetValue(key, out var job) && job.CreatedUtc < cutoff)
            {
                _jobs.TryRemove(key, out _);
                job.Cancellation.Dispose();
            }
        }
    }
}
