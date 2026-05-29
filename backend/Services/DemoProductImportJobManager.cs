using System.Collections.Concurrent;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Hubs;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.SignalR;

namespace KasseAPI_Final.Services;

public interface IDemoProductImportJobManager
{
    Task<DemoImportJobStartResponseDto> StartCatalogImportAsync(
        Guid tenantId,
        DemoImportRequest request,
        ClaimsPrincipal actor,
        CancellationToken requestAborted = default);

    DemoImportProgressDto? GetProgress(string jobId);

    DemoImportJobStatusDto? GetStatus(string jobId);

    bool TryAuthorizeSubscription(ClaimsPrincipal? user, string jobId);

    bool TryCancel(string jobId);
}

public sealed class DemoProductImportJobEntry
{
    private readonly object _lock = new();

    public string JobId { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public bool ActorIsSuperAdmin { get; init; }
    public DemoImportRequest Request { get; init; } = new();
    public DemoImportJobStatus Status { get; set; } = DemoImportJobStatus.Queued;
    public DemoImportProgressDto Progress { get; set; } = new();
    public CancellationTokenSource Cancellation { get; init; } = new();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    public void UpdateProgress(DemoImportProgressDto progress)
    {
        lock (_lock)
        {
            Progress = progress;
        }
    }

    public DemoImportProgressDto SnapshotProgress()
    {
        lock (_lock)
        {
            return Progress;
        }
    }
}

/// <summary>In-memory demo product import jobs with SignalR progress pushes.</summary>
public sealed class DemoProductImportJobManager : IDemoProductImportJobManager
{
    public static string GroupName(string jobId) => $"demo-import:{jobId}";

    private static readonly TimeSpan JobTtl = TimeSpan.FromHours(2);
    private readonly ConcurrentDictionary<string, DemoProductImportJobEntry> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DemoImportProgressHub> _hubContext;
    private readonly ILogger<DemoProductImportJobManager> _logger;

    public DemoProductImportJobManager(
        IServiceScopeFactory scopeFactory,
        IHubContext<DemoImportProgressHub> hubContext,
        ILogger<DemoProductImportJobManager> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task<DemoImportJobStartResponseDto> StartCatalogImportAsync(
        Guid tenantId,
        DemoImportRequest request,
        ClaimsPrincipal actor,
        CancellationToken requestAborted = default)
    {
        PurgeStaleJobs();

        var actorUserId = actor.GetActorUserId();
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new InvalidOperationException("Authenticated user required to start demo import.");

        var jobId = Guid.NewGuid().ToString("N");
        var entry = new DemoProductImportJobEntry
        {
            JobId = jobId,
            TenantId = tenantId,
            ActorUserId = actorUserId,
            ActorIsSuperAdmin = actor.IsInRole(Roles.SuperAdmin),
            Request = request,
            Status = DemoImportJobStatus.Queued,
            Progress = new DemoImportProgressDto(
                JobId: jobId,
                Status: DemoImportJobStatus.Queued,
                Message: "Import wird vorbereitet…"),
        };
        _jobs[jobId] = entry;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                tenantAccessor.TenantId = tenantId;

                var importService = scope.ServiceProvider.GetRequiredService<IDemoProductImportService>();
                var progress = new Progress<DemoImportProgressDto>(dto =>
                {
                    dto = dto with { JobId = jobId };
                    entry.Status = dto.Status;
                    entry.UpdateProgress(dto);
                    _ = PublishProgressAsync(jobId, dto);
                });

                entry.Status = DemoImportJobStatus.Running;
                PublishProgressAsync(jobId, entry.SnapshotProgress() with
                {
                    Status = DemoImportJobStatus.Running,
                    Message = "Import läuft…",
                }).GetAwaiter().GetResult();

                var result = await importService
                    .ImportDemoProductsAsync(tenantId, request, progress, entry.Cancellation.Token)
                    .ConfigureAwait(false);

                var terminalStatus = result.Success
                    ? DemoImportJobStatus.Completed
                    : DemoImportJobStatus.Failed;

                var final = BuildFinalProgress(jobId, entry.SnapshotProgress(), terminalStatus, result);
                entry.Status = terminalStatus;
                entry.UpdateProgress(final);
                await PublishProgressAsync(jobId, final).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                var cancelled = entry.SnapshotProgress() with
                {
                    JobId = jobId,
                    Status = DemoImportJobStatus.Cancelled,
                    Message = "Import abgebrochen.",
                };
                entry.Status = DemoImportJobStatus.Cancelled;
                entry.UpdateProgress(cancelled);
                await PublishProgressAsync(jobId, cancelled).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo import job {JobId} crashed", jobId);
                var failed = entry.SnapshotProgress() with
                {
                    JobId = jobId,
                    Status = DemoImportJobStatus.Failed,
                    Message = "Import ist unerwartet fehlgeschlagen.",
                };
                entry.Status = DemoImportJobStatus.Failed;
                entry.UpdateProgress(failed);
                await PublishProgressAsync(jobId, failed).ConfigureAwait(false);
            }
        }, CancellationToken.None);

        return Task.FromResult(new DemoImportJobStartResponseDto(jobId, 0));
    }

    public DemoImportProgressDto? GetProgress(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var entry) ? entry.SnapshotProgress() : null;
    }

    public DemoImportJobStatusDto? GetStatus(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return null;

        return new DemoImportJobStatusDto(entry.JobId, entry.Status, entry.SnapshotProgress());
    }

    public bool TryAuthorizeSubscription(ClaimsPrincipal? user, string jobId)
    {
        if (user == null || !_jobs.TryGetValue(jobId, out var entry))
            return false;

        var actorUserId = user.GetActorUserId();
        if (string.IsNullOrWhiteSpace(actorUserId))
            return false;

        if (string.Equals(actorUserId, entry.ActorUserId, StringComparison.Ordinal))
            return true;

        return entry.ActorIsSuperAdmin && user.IsInRole(Roles.SuperAdmin);
    }

    public bool TryCancel(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var entry))
            return false;

        if (entry.Status is DemoImportJobStatus.Completed or DemoImportJobStatus.Cancelled or DemoImportJobStatus.Failed)
            return false;

        entry.Cancellation.Cancel();
        return true;
    }

    private async Task PublishProgressAsync(string jobId, DemoImportProgressDto progress)
    {
        try
        {
            await _hubContext.Clients
                .Group(GroupName(jobId))
                .SendAsync("ImportProgress", progress)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push demo import progress for job {JobId}", jobId);
        }
    }

    private static DemoImportProgressDto BuildFinalProgress(
        string jobId,
        DemoImportProgressDto current,
        DemoImportJobStatus status,
        ImportResult result)
    {
        var categories = current.Categories
            .Select(c => c with { State = "Completed", Processed = c.Total })
            .ToList();

        return current with
        {
            JobId = jobId,
            Status = status,
            Result = result,
            Message = result.Success ? null : result.ErrorMessage,
            Percent = current.TotalProducts > 0 ? 100 : 0,
            ProcessedProducts = current.TotalProducts,
            Categories = categories,
            CurrentProductName = null,
        };
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
