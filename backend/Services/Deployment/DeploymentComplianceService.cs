using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Deployment;

public interface IDeploymentComplianceService
{
    Task<DeploymentComplianceSignoffDto> SignOffAsync(
        DeploymentComplianceSignoffRequest request,
        string actorUserId,
        string actorRole,
        string? actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<DeploymentComplianceGateStatusDto> GetGateStatusAsync(
        string imageTag,
        string stage = "production",
        CancellationToken cancellationToken = default);

    Task<DeploymentComplianceSignoffDto?> GetLatestSignoffAsync(
        string imageTag,
        string stage = "production",
        CancellationToken cancellationToken = default);
}

public sealed class DeploymentComplianceService : IDeploymentComplianceService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IDeploymentAuditService _audit;
    private readonly ILogger<DeploymentComplianceService> _logger;

    public DeploymentComplianceService(
        IDbContextFactory<AppDbContext> dbFactory,
        IDeploymentAuditService audit,
        ILogger<DeploymentComplianceService> logger)
    {
        _dbFactory = dbFactory;
        _audit = audit;
        _logger = logger;
    }

    public async Task<DeploymentComplianceSignoffDto> SignOffAsync(
        DeploymentComplianceSignoffRequest request,
        string actorUserId,
        string actorRole,
        string? actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ImageTag))
            throw new ArgumentException("ImageTag is required.");

        var missing = GetMissingItems(request.Checklist);
        if (missing.Count > 0)
            throw new ArgumentException(
                "All compliance checklist items must be true: " + string.Join(", ", missing));

        var stage = string.IsNullOrWhiteSpace(request.Stage)
            ? "production"
            : request.Stage.Trim().ToLowerInvariant();
        if (stage is not ("production" or "canary"))
            throw new ArgumentException("Stage must be production or canary.");

        var hours = Math.Clamp(request.ValidHours ?? 72, 1, 168);
        var now = DateTime.UtcNow;
        var row = new DeploymentComplianceSignoff
        {
            Id = Guid.NewGuid(),
            ImageTag = request.ImageTag.Trim(),
            GitSha = string.IsNullOrWhiteSpace(request.GitSha) ? null : request.GitSha.Trim(),
            Stage = stage,
            ChecklistJson = JsonSerializer.Serialize(request.Checklist, JsonOpts),
            SignedByUserId = actorUserId,
            SignedByRole = actorRole,
            SignedByDisplayName = actorDisplayName,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SignedAtUtc = now,
            ExpiresAtUtc = now.AddHours(hours),
        };

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.DeploymentComplianceSignoffs.Add(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _audit.LogComplianceApprovedAsync(row, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Compliance sign-off recorded: image={Image} by={Actor} stage={Stage}",
            row.ImageTag, actorUserId, stage);

        return ToDto(row, now);
    }

    public async Task<DeploymentComplianceGateStatusDto> GetGateStatusAsync(
        string imageTag,
        string stage = "production",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageTag))
            throw new ArgumentException("ImageTag is required.");

        var latest = await GetLatestSignoffAsync(imageTag, stage, cancellationToken).ConfigureAwait(false);
        var missing = latest is null
            ? GetMissingItems(new DeploymentComplianceChecklistDto())
            : GetMissingItems(latest.Checklist);

        var checklistComplete = missing.Count == 0;
        var signoffValid = latest?.IsValid == true;
        return new DeploymentComplianceGateStatusDto
        {
            CheckedAtUtc = DateTime.UtcNow,
            ImageTag = imageTag.Trim(),
            Stage = string.IsNullOrWhiteSpace(stage) ? "production" : stage.Trim().ToLowerInvariant(),
            SignoffPresent = latest is not null,
            SignoffValid = signoffValid,
            ChecklistComplete = checklistComplete,
            GatePassed = signoffValid && checklistComplete,
            LatestSignoff = latest,
            MissingChecklistItems = missing,
        };
    }

    public async Task<DeploymentComplianceSignoffDto?> GetLatestSignoffAsync(
        string imageTag,
        string stage = "production",
        CancellationToken cancellationToken = default)
    {
        var tag = imageTag.Trim();
        var st = string.IsNullOrWhiteSpace(stage) ? "production" : stage.Trim().ToLowerInvariant();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.DeploymentComplianceSignoffs.AsNoTracking()
            .Where(s => s.ImageTag == tag && s.Stage == st)
            .OrderByDescending(s => s.SignedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToDto(row, DateTime.UtcNow);
    }

    internal static IReadOnlyList<string> GetMissingItems(DeploymentComplianceChecklistDto checklist)
    {
        var missing = new List<string>();
        if (!checklist.DepExportTested) missing.Add("depExportTested");
        if (!checklist.TseSignatureTested) missing.Add("tseSignatureTested");
        if (!checklist.FinanzOnlineTestSubmission) missing.Add("finanzOnlineTestSubmission");
        if (!checklist.NtpTimeSyncChecked) missing.Add("ntpTimeSyncChecked");
        if (!checklist.TenantIsolationVerified) missing.Add("tenantIsolationVerified");
        return missing;
    }

    private static DeploymentComplianceSignoffDto ToDto(DeploymentComplianceSignoff row, DateTime now)
    {
        DeploymentComplianceChecklistDto checklist;
        try
        {
            checklist = JsonSerializer.Deserialize<DeploymentComplianceChecklistDto>(row.ChecklistJson, JsonOpts)
                        ?? new DeploymentComplianceChecklistDto();
        }
        catch
        {
            checklist = new DeploymentComplianceChecklistDto();
        }

        var valid = GetMissingItems(checklist).Count == 0
                    && (row.ExpiresAtUtc is null || row.ExpiresAtUtc > now);

        return new DeploymentComplianceSignoffDto
        {
            Id = row.Id,
            ImageTag = row.ImageTag,
            GitSha = row.GitSha,
            Stage = row.Stage,
            Checklist = checklist,
            SignedByUserId = row.SignedByUserId,
            SignedByRole = row.SignedByRole,
            SignedByDisplayName = row.SignedByDisplayName,
            Notes = row.Notes,
            SignedAtUtc = row.SignedAtUtc,
            ExpiresAtUtc = row.ExpiresAtUtc,
            IsValid = valid,
        };
    }
}
