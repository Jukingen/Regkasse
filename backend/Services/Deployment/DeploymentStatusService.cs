using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Deployment;

public sealed class DeploymentStatusService : IDeploymentStatusService
{
    private static readonly HashSet<string> AllowedStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "staging", "canary", "production",
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending", "deploying", "smoke_running", "succeeded", "failed", "rolled_back",
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<DeploymentStatusService> _logger;

    public DeploymentStatusService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<DeploymentStatusService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<DeploymentRunDto> ReportAsync(
        DeploymentCiReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stage = NormalizeStage(request.Stage);
        var status = NormalizeStatus(request.Status);
        var now = DateTime.UtcNow;
        var tenantJson = SerializeTenants(request.TenantIds);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DeploymentRun? existing = null;
        if (!string.IsNullOrWhiteSpace(request.RunUrl))
        {
            existing = await db.DeploymentRuns
                .Where(r => r.RunUrl == request.RunUrl && r.Stage == stage)
                .OrderByDescending(r => r.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (existing is null)
        {
            existing = new DeploymentRun
            {
                Id = Guid.NewGuid(),
                Stage = stage,
                CreatedAtUtc = now,
            };
            db.DeploymentRuns.Add(existing);
        }

        existing.Status = status;
        existing.GitSha = Truncate(request.GitSha, 64);
        existing.GitRef = Truncate(request.GitRef, 256);
        existing.ImageTag = Truncate(request.ImageTag, 512);
        existing.TenantIdsJson = tenantJson;
        existing.ErrorMessage = Truncate(request.ErrorMessage, 2000);
        existing.RunUrl = Truncate(request.RunUrl, 1024);
        existing.TriggeredBy = Truncate(request.TriggeredBy, 200);
        if (request.SmokePassed.HasValue)
            existing.SmokePassed = request.SmokePassed;
        if (request.SmokeSummary is not null)
            existing.SmokeSummary = Truncate(request.SmokeSummary, 2000);
        if (!string.IsNullOrWhiteSpace(request.PreviousImageTag))
            existing.PreviousImageTag = Truncate(request.PreviousImageTag, 512);
        existing.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Deployment status recorded: stage={Stage} status={Status} sha={Sha} image={Image}",
            stage, status, existing.GitSha, existing.ImageTag);

        return ToDto(existing);
    }

    public async Task<DeploymentRunListResponseDto> ListAsync(
        string? stage = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = db.DeploymentRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(stage))
        {
            var normalized = NormalizeStage(stage);
            query = query.Where(r => r.Stage == normalized);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(ToDto).ToList();

        var latestByStage = new Dictionary<string, DeploymentRunDto?>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in AllowedStages)
        {
            var latest = await db.DeploymentRuns.AsNoTracking()
                .Where(r => r.Stage == s)
                .OrderByDescending(r => r.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            latestByStage[s] = latest is null ? null : ToDto(latest);
        }

        return new DeploymentRunListResponseDto
        {
            Items = items,
            Total = total,
            LatestByStage = latestByStage,
        };
    }

    private static string NormalizeStage(string? stage)
    {
        var s = (stage ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedStages.Contains(s))
            throw new ArgumentException($"Invalid stage '{stage}'. Expected staging|canary|production.");
        return s;
    }

    private static string NormalizeStatus(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedStatuses.Contains(s))
            throw new ArgumentException(
                $"Invalid status '{status}'. Expected pending|deploying|smoke_running|succeeded|failed|rolled_back.");
        return s;
    }

    private static string? SerializeTenants(IReadOnlyList<string>? tenantIds)
    {
        if (tenantIds is null || tenantIds.Count == 0)
            return null;
        var cleaned = tenantIds
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();
        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned);
    }

    private static IReadOnlyList<string> DeserializeTenants(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static DeploymentRunDto ToDto(DeploymentRun row) => new()
    {
        Id = row.Id,
        Stage = row.Stage,
        Status = row.Status,
        GitSha = row.GitSha,
        GitRef = row.GitRef,
        ImageTag = row.ImageTag,
        PreviousImageTag = row.PreviousImageTag,
        TenantIds = DeserializeTenants(row.TenantIdsJson),
        ErrorMessage = row.ErrorMessage,
        RunUrl = row.RunUrl,
        TriggeredBy = row.TriggeredBy,
        SmokePassed = row.SmokePassed,
        SmokeSummary = row.SmokeSummary,
        CreatedAtUtc = row.CreatedAtUtc,
        UpdatedAtUtc = row.UpdatedAtUtc,
    };

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }
}
