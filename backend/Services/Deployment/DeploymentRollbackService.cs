using System.Net.Http.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Deployment;

public sealed class DeploymentRollbackService : IDeploymentRollbackService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly DeploymentOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DeploymentRollbackService> _logger;

    public DeploymentRollbackService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptions<DeploymentOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<DeploymentRollbackService> logger)
    {
        _dbFactory = dbFactory;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DeploymentRollbackResultDto> RollbackAsync(
        DeploymentRollbackRequest request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Confirm, "rollback", StringComparison.Ordinal))
            throw new ArgumentException("Confirm must be exactly 'rollback'.");

        var stage = (request.Stage ?? string.Empty).Trim().ToLowerInvariant();
        if (stage is not ("staging" or "canary" or "production"))
            throw new ArgumentException("Stage must be staging|canary|production.");

        if (!_options.RollbackWebhooks.TryGetValue(stage, out var webhook) ||
            string.IsNullOrWhiteSpace(webhook))
        {
            throw new InvalidOperationException(
                $"Deployment:RollbackWebhooks:{stage} is not configured.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var latest = await db.DeploymentRuns
            .Where(r => r.Stage == stage)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var previous = Truncate(
            request.PreviousImageTag
            ?? latest?.PreviousImageTag
            ?? string.Empty,
            512);

        if (string.IsNullOrWhiteSpace(previous))
            throw new InvalidOperationException(
                "No previousImageTag available. Pass previousImageTag in the request.");

        var failedImage = latest?.ImageTag;
        var client = _httpClientFactory.CreateClient("deployment-rollback");
        using var response = await client.PostAsJsonAsync(
            webhook.Trim(),
            new
            {
                action = "rollback",
                stage,
                previousImage = previous,
                failedImage,
                triggeredBy = actor,
                source = "admin-ui",
            },
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Rollback webhook failed for {Stage}: {Status} {Body}",
                stage, (int)response.StatusCode, Truncate(body, 500));
            throw new InvalidOperationException(
                $"Rollback webhook returned HTTP {(int)response.StatusCode}.");
        }

        var now = DateTime.UtcNow;
        db.DeploymentRuns.Add(new Models.DeploymentRun
        {
            Id = Guid.NewGuid(),
            Stage = stage,
            Status = "rolled_back",
            ImageTag = previous,
            PreviousImageTag = failedImage,
            TriggeredBy = Truncate(actor, 200),
            ErrorMessage = "Manual rollback from FA /admin/deployments",
            SmokePassed = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "Manual deployment rollback invoked by {Actor} for stage={Stage} previousImage={Image}",
            actor, stage, previous);

        return new DeploymentRollbackResultDto
        {
            Invoked = true,
            Stage = stage,
            PreviousImageTag = previous,
            Message = "Rollback webhook invoked. Verify smoke on the target stage.",
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
