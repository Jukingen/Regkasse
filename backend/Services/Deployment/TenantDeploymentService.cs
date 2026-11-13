using System.Net.Http.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Deployment;

public sealed class TenantDeploymentService : ITenantDeploymentService
{
    private static readonly HashSet<string> AllowedStages = new(StringComparer.OrdinalIgnoreCase)
    {
        "staging", "canary", "production",
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending", "deploying", "succeeded", "failed", "rolled_back", "canary_soak", "promoted",
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly DeploymentOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TenantDeploymentService> _logger;

    public TenantDeploymentService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptions<DeploymentOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<TenantDeploymentService> logger)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DeploymentOverallStatusDto> GetOverallStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await ListLatestPerTenantAsync(cancellationToken).ConfigureAwait(false);
        var soaking = tenants.Where(t => t.IsCanarySoaking).ToList();
        var failed = tenants.Count(t =>
            string.Equals(t.Status, "failed", StringComparison.OrdinalIgnoreCase));

        string? nextSlug = null;
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var configured = _options.CanaryTenantSlugs
            .Concat(_options.CanaryTenantIds.Select(g => g.ToString("D")))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (configured.Count > 0)
        {
            var soakingOrSucceeded = tenants
                .Where(t =>
                    string.Equals(t.Stage, "canary", StringComparison.OrdinalIgnoreCase)
                    && (t.IsCanarySoaking
                        || string.Equals(t.Status, "succeeded", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t.Status, "canary_soak", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t.Status, "promoted", StringComparison.OrdinalIgnoreCase)))
                .Select(t => t.TenantSlug ?? t.TenantId.ToString("D"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            nextSlug = configured.FirstOrDefault(c => !soakingOrSucceeded.Contains(c.Trim()));
        }

        return new DeploymentOverallStatusDto
        {
            CheckedAtUtc = DateTime.UtcNow,
            Tenants = tenants,
            CanarySoakingCount = soaking.Count,
            FailedCount = failed,
            RecommendedNextCanaryTenantSlug = nextSlug,
        };
    }

    public async Task<IReadOnlyList<TenantDeploymentHistoryDto>> ListLatestPerTenantAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Latest row per tenant
        var latestIds = await db.TenantDeploymentHistories.AsNoTracking()
            .GroupBy(h => h.TenantId)
            .Select(g => g.OrderByDescending(x => x.DeployedAtUtc).Select(x => x.Id).First())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await db.TenantDeploymentHistories.AsNoTracking()
            .Include(h => h.Tenant)
            .Where(h => latestIds.Contains(h.Id))
            .OrderBy(h => h.Tenant!.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDto).ToList();
    }

    public async Task<TenantDeploymentHistoryDto?> GetLatestForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantDeploymentHistories.AsNoTracking()
            .Include(h => h.Tenant)
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.DeployedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDto(row);
    }

    public async Task<TenantDeploymentHistoryDto> RecordAsync(
        TenantDeploymentRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stage = NormalizeStage(request.Stage);
        var status = NormalizeStatus(request.Status);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var tenant = await ResolveTenantAsync(db, request.TenantIdOrSlug, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ArgumentException($"Tenant '{request.TenantIdOrSlug}' not found.");

        var previous = request.PreviousVersion;
        if (string.IsNullOrWhiteSpace(previous))
        {
            previous = await db.TenantDeploymentHistories.AsNoTracking()
                .Where(h => h.TenantId == tenant.Id)
                .OrderByDescending(h => h.DeployedAtUtc)
                .Select(h => h.Version)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var now = DateTime.UtcNow;
        var soakHours = request.SoakHours
            ?? _options.CanaryDefaultSoakHours;
        soakHours = Math.Clamp(soakHours, 1, 168);

        DateTime? soakUntil = null;
        if (stage == "canary" &&
            (status is "succeeded" or "canary_soak"))
        {
            status = "canary_soak";
            soakUntil = now.AddHours(soakHours);
        }

        var row = new TenantDeploymentHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Version = Truncate(request.Version, 512)!,
            PreviousVersion = Truncate(previous, 512),
            Stage = stage,
            Status = status,
            GitSha = Truncate(request.GitSha, 64),
            RunUrl = Truncate(request.RunUrl, 1024),
            TriggeredBy = Truncate(request.TriggeredBy, 200),
            ErrorMessage = Truncate(request.ErrorMessage, 2000),
            SmokePassed = request.SmokePassed,
            DeployedAtUtc = now,
            SoakUntilUtc = soakUntil,
            UpdatedAtUtc = now,
        };
        db.TenantDeploymentHistories.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        row.Tenant = tenant;
        _logger.LogInformation(
            "Tenant deployment recorded: tenant={Slug} version={Version} stage={Stage} status={Status}",
            tenant.Slug, row.Version, row.Stage, row.Status);
        return ToDto(row);
    }

    public async Task RecordFromCiAsync(
        IReadOnlyList<string> tenantIdsOrSlugs,
        string version,
        string stage,
        string status,
        string? gitSha,
        string? runUrl,
        string? triggeredBy,
        bool? smokePassed,
        string? errorMessage,
        int? soakHours,
        CancellationToken cancellationToken = default)
    {
        foreach (var raw in tenantIdsOrSlugs)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (string.Equals(raw.Trim(), "smoke", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                await RecordAsync(new TenantDeploymentRecordRequest
                {
                    TenantIdOrSlug = raw.Trim(),
                    Version = version,
                    Stage = stage,
                    Status = status,
                    GitSha = gitSha,
                    RunUrl = runUrl,
                    TriggeredBy = triggeredBy,
                    SmokePassed = smokePassed,
                    ErrorMessage = errorMessage,
                    SoakHours = soakHours,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record CI deployment for tenant key {Key}", raw);
            }
        }
    }

    public async Task<DeploymentRollbackResultDto> RollbackTenantAsync(
        Guid tenantId,
        TenantDeploymentRollbackRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.Confirm, "rollback", StringComparison.Ordinal))
            throw new ArgumentException("Confirm must be exactly 'rollback'.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var latest = await db.TenantDeploymentHistories
            .Include(h => h.Tenant)
            .Where(h => h.TenantId == tenantId)
            .OrderByDescending(h => h.DeployedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("No deployment history for this tenant.");

        var previous = Truncate(
            request.PreviousVersion ?? latest.PreviousVersion,
            512);
        if (string.IsNullOrWhiteSpace(previous))
            throw new InvalidOperationException("No previousVersion available for rollback.");

        var stage = latest.Stage;
        if (!_options.RollbackWebhooks.TryGetValue(stage, out var webhook) ||
            string.IsNullOrWhiteSpace(webhook))
        {
            // Fall back to canary webhook for tenant-scoped canary rollbacks
            _options.RollbackWebhooks.TryGetValue("canary", out webhook);
        }

        if (string.IsNullOrWhiteSpace(webhook))
            throw new InvalidOperationException(
                $"No RollbackWebhooks configured for stage '{stage}' (or canary).");

        var client = _httpClientFactory.CreateClient("deployment-rollback");
        using var response = await client.PostAsJsonAsync(
            webhook.Trim(),
            new
            {
                action = "rollback",
                stage,
                previousImage = previous,
                failedImage = latest.Version,
                tenantId = tenantId.ToString("D"),
                tenantSlug = latest.Tenant?.Slug,
                triggeredBy = actor,
                source = "admin-ui-tenant",
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Rollback webhook returned HTTP {(int)response.StatusCode}.");
        }

        var now = DateTime.UtcNow;
        db.TenantDeploymentHistories.Add(new TenantDeploymentHistory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Version = previous,
            PreviousVersion = latest.Version,
            Stage = stage,
            Status = "rolled_back",
            TriggeredBy = Truncate(actor, 200),
            ErrorMessage = "Tenant rollback from FA /admin/deployments/tenants",
            DeployedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "Tenant rollback by {Actor}: tenant={TenantId} previous={Version}",
            actor, tenantId, previous);

        return new DeploymentRollbackResultDto
        {
            Invoked = true,
            Stage = stage,
            PreviousImageTag = previous,
            Message = $"Tenant rollback webhook invoked for {latest.Tenant?.Slug ?? tenantId.ToString("D")}.",
        };
    }

    private static async Task<Tenant?> ResolveTenantAsync(
        AppDbContext db,
        string idOrSlug,
        CancellationToken cancellationToken)
    {
        var key = idOrSlug.Trim();
        if (Guid.TryParse(key, out var id))
        {
            return await db.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }

        return await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == key, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string NormalizeStage(string? stage)
    {
        var s = (stage ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedStages.Contains(s))
            throw new ArgumentException($"Invalid stage '{stage}'.");
        return s;
    }

    private static string NormalizeStatus(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedStatuses.Contains(s))
            throw new ArgumentException($"Invalid status '{status}'.");
        return s;
    }

    private static TenantDeploymentHistoryDto ToDto(TenantDeploymentHistory row)
    {
        var now = DateTime.UtcNow;
        var soaking = string.Equals(row.Status, "canary_soak", StringComparison.OrdinalIgnoreCase)
            && row.SoakUntilUtc is { } until
            && until > now;

        return new TenantDeploymentHistoryDto
        {
            Id = row.Id,
            TenantId = row.TenantId,
            TenantSlug = row.Tenant?.Slug,
            TenantName = row.Tenant?.Name,
            Version = row.Version,
            PreviousVersion = row.PreviousVersion,
            Stage = row.Stage,
            Status = row.Status,
            GitSha = row.GitSha,
            RunUrl = row.RunUrl,
            TriggeredBy = row.TriggeredBy,
            ErrorMessage = row.ErrorMessage,
            SmokePassed = row.SmokePassed,
            DeployedAtUtc = row.DeployedAtUtc,
            SoakUntilUtc = row.SoakUntilUtc,
            UpdatedAtUtc = row.UpdatedAtUtc,
            IsCanarySoaking = soaking,
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }
}
