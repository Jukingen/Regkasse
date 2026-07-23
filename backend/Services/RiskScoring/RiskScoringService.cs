using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.RiskScoring;

public sealed class RiskScoringService : IRiskScoringService
{
    private const int PersistThreshold = 30;

    private readonly AppDbContext _db;

    public RiskScoringService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public RiskScore CalculateRisk(UserAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var score = 0;
        var reasons = new List<string>();

        // Rule 1: Unusual time (3 AM - 5 AM, inclusive on hour)
        if (action.Timestamp.Hour is >= 3 and <= 5)
        {
            score += 20;
            reasons.Add("Unusual time: 3 AM - 5 AM");
        }

        // Rule 2: Bulk operation > 3x normal average
        if (action.AverageBulkCount > 0 && action.BulkCount > action.AverageBulkCount * 3)
        {
            score += 30;
            reasons.Add($"Bulk operation: {action.BulkCount} items (avg: {action.AverageBulkCount:0.##})");
        }

        // Rule 3: New IP address
        if (!action.IsKnownIp)
        {
            score += 15;
            reasons.Add("New IP address");
        }

        // Rule 4: Rapid succession
        if (action.IsRapidSuccession)
        {
            score += 25;
            reasons.Add("Rapid succession of actions");
        }

        // Rule 5: First time action
        if (action.IsFirstTime)
        {
            score += 10;
            reasons.Add("First time performing this action");
        }

        score = Math.Min(score, 100);
        var level = score switch
        {
            >= 70 => RiskLevels.Critical,
            >= 50 => RiskLevels.High,
            >= 30 => RiskLevels.Medium,
            _ => RiskLevels.Low,
        };

        return new RiskScore
        {
            TenantId = action.TenantId,
            UserId = action.UserId ?? string.Empty,
            ActionType = action.ActionType ?? string.Empty,
            Score = score,
            RiskLevel = level,
            Reason = reasons.Count == 0 ? "No elevated risk signals" : string.Join("; ", reasons),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
    }

    /// <inheritdoc />
    public async Task<EvaluateUserActionResponseDto> EvaluateAsync(
        UserAction action,
        bool persistIfElevated = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var calculated = CalculateRisk(action);
        Guid? persistedId = null;

        if (persistIfElevated && calculated.Score >= PersistThreshold)
        {
            calculated.DetailsJson = JsonSerializer.Serialize(new
            {
                action.IpAddress,
                action.CorrelationId,
                action.BulkCount,
                action.AverageBulkCount,
                action.IsKnownIp,
                action.IsRapidSuccession,
                action.IsFirstTime,
                Timestamp = action.Timestamp,
            });

            _db.RiskScores.Add(calculated);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            persistedId = calculated.Id;
        }

        return new EvaluateUserActionResponseDto
        {
            Score = calculated.Score,
            RiskLevel = calculated.RiskLevel,
            Reason = calculated.Reason,
            PersistedId = persistedId,
        };
    }

    /// <inheritdoc />
    public async Task<RiskScoreListResponseDto> ListAsync(
        bool unresolvedOnly,
        string? riskLevel,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        offset = Math.Max(0, offset);

        var query = _db.RiskScores.AsNoTracking()
            .IgnoreQueryFilters()
            .AsQueryable();

        if (unresolvedOnly)
            query = query.Where(r => !r.IsResolved);

        if (!string.IsNullOrWhiteSpace(riskLevel) && RiskLevels.IsValid(riskLevel.Trim()))
            query = query.Where(r => r.RiskLevel == riskLevel.Trim());

        var summarySource = await _db.RiskScores.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(r => !r.IsResolved)
            .GroupBy(r => r.RiskLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summary = new RiskScoreSummaryDto
        {
            Critical = summarySource.FirstOrDefault(x => x.Level == RiskLevels.Critical)?.Count ?? 0,
            High = summarySource.FirstOrDefault(x => x.Level == RiskLevels.High)?.Count ?? 0,
            Medium = summarySource.FirstOrDefault(x => x.Level == RiskLevels.Medium)?.Count ?? 0,
            Low = summarySource.FirstOrDefault(x => x.Level == RiskLevels.Low)?.Count ?? 0,
        };
        summary.Open = summary.Critical + summary.High + summary.Medium + summary.Low;

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();

        var tenants = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken)
            .ConfigureAwait(false);

        var users = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => new { u.Email, u.UserName },
                cancellationToken)
            .ConfigureAwait(false);

        return new RiskScoreListResponseDto
        {
            Total = total,
            Summary = summary,
            Items = rows.Select(r =>
            {
                users.TryGetValue(r.UserId, out var user);
                tenants.TryGetValue(r.TenantId, out var tenantName);
                return Map(r, tenantName, user?.Email, user?.UserName);
            }).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<RiskScoreDto?> ResolveAsync(
        Guid id,
        string resolvedByUserId,
        string resolution,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resolution))
            throw new ArgumentException("Resolution is required.", nameof(resolution));

        var entity = await _db.RiskScores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        if (entity.IsResolved)
            return await ToDtoAsync(entity, cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        entity.IsResolved = true;
        entity.ResolvedAt = now;
        entity.ResolvedBy = resolvedByUserId;
        entity.Resolution = resolution.Trim();
        entity.UpdatedAt = now;
        entity.UpdatedBy = resolvedByUserId;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtoAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RiskScoreDto> ToDtoAsync(RiskScore entity, CancellationToken cancellationToken)
    {
        var tenantName = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == entity.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == entity.UserId)
            .Select(u => new { u.Email, u.UserName })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return Map(entity, tenantName, user?.Email, user?.UserName);
    }

    private static RiskScoreDto Map(
        RiskScore entity,
        string? tenantName,
        string? email,
        string? userName) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TenantName = tenantName,
            UserId = entity.UserId,
            UserEmail = email,
            UserName = userName,
            ActionType = entity.ActionType,
            Score = entity.Score,
            RiskLevel = entity.RiskLevel,
            Reason = entity.Reason,
            CreatedAt = entity.CreatedAt,
            IsResolved = entity.IsResolved,
            ResolvedAt = entity.ResolvedAt,
            ResolvedBy = entity.ResolvedBy,
            Resolution = entity.Resolution,
        };
}
