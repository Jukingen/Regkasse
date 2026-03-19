using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Read-only analysis and idempotent repair of legacy payload_hash values that do not match
/// runtime canonical SHA-256 (sorted JSON keys). Safe to run multiple times.
/// </summary>
public interface IOfflinePayloadHashMaintenanceService
{
    Task<OfflinePayloadHashAnalyzeResult> AnalyzeAsync(
        int maxRows,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<OfflinePayloadHashRepairResult> RepairAsync(
        int maxRows,
        bool dryRun,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);
}

public sealed class OfflinePayloadHashAnalyzeResult
{
    public int Scanned { get; init; }
    public int NullOrEmptyPayloadHash { get; init; }
    public int RuntimeMismatchCount { get; init; }
    public int RepairableNoConflictCount { get; init; }
    public int SkippedWouldConflictCount { get; init; }
    public IReadOnlyList<Guid> SampleMismatchIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Mismatch ratio as percent (100.0 * RuntimeMismatchCount / Scanned when Scanned &gt; 0).</summary>
    public double MismatchRatioPercent { get; init; }

    /// <summary>True when MismatchRatioPercent exceeds configured threshold; indicates legacy data quality risk.</summary>
    public bool LegacyDataQualityRiskHigh { get; init; }

    /// <summary>Set when LegacyDataQualityRiskHigh is true; message for ops/UI.</summary>
    public string? WarningMessage { get; init; }
}

public sealed class OfflinePayloadHashRepairResult
{
    public int Scanned { get; init; }
    public int Updated { get; init; }
    public int SkippedConflict { get; init; }
    public int SkippedAlreadyAligned { get; init; }
    public int SkippedNullPayload { get; init; }
    public int SkippedNormalizeError { get; init; }
    public bool DryRun { get; init; }
}

public sealed class OfflinePayloadHashMaintenanceService : IOfflinePayloadHashMaintenanceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<OfflinePayloadHashMaintenanceService> _logger;
    private readonly PayloadHashGuardOptions _guardOptions;
    private readonly ICoreMetrics? _metrics;

    public OfflinePayloadHashMaintenanceService(
        AppDbContext context,
        ILogger<OfflinePayloadHashMaintenanceService> logger,
        IOptionsMonitor<PayloadHashGuardOptions>? guardOptions = null,
        ICoreMetrics? metrics = null)
    {
        _context = context;
        _logger = logger;
        _guardOptions = guardOptions?.CurrentValue ?? new PayloadHashGuardOptions();
        _metrics = metrics;
    }

    public async Task<OfflinePayloadHashAnalyzeResult> AnalyzeAsync(
        int maxRows,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        maxRows = Math.Clamp(maxRows, 1, 100_000);
        var query = _context.OfflineTransactions.AsNoTracking().AsQueryable();
        if (cashRegisterId.HasValue)
            query = query.Where(x => x.CashRegisterId == cashRegisterId.Value);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(maxRows)
            .Select(x => new { x.Id, x.CashRegisterId, x.PayloadJson, x.PayloadHash })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nullHash = 0;
        var mismatchItems = new List<(Guid Id, Guid CashRegisterId, string Canonical)>();
        var samples = new List<Guid>(20);

        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.PayloadHash))
            {
                nullHash++;
                continue;
            }

            string canonical;
            try
            {
                canonical = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(r.PayloadJson ?? "{}");
            }
            catch
            {
                continue;
            }

            if (string.Equals(r.PayloadHash.Trim(), canonical, StringComparison.OrdinalIgnoreCase))
                continue;

            mismatchItems.Add((r.Id, r.CashRegisterId, canonical));
            if (samples.Count < 20)
                samples.Add(r.Id);
        }

        var repairable = 0;
        var conflict = 0;
        if (mismatchItems.Count > 0)
        {
            var regIds = mismatchItems.Select(m => m.CashRegisterId).Distinct().ToList();
            var canonHashes = mismatchItems.Select(m => m.Canonical).Distinct().ToList();
            var occupants = await _context.OfflineTransactions.AsNoTracking()
                .Where(x =>
                    regIds.Contains(x.CashRegisterId) &&
                    x.PayloadHash != null &&
                    canonHashes.Contains(x.PayloadHash))
                .Select(x => new { x.Id, x.CashRegisterId, x.PayloadHash })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var byRegHash = occupants
                .GroupBy(o => (o.CashRegisterId, Hash: o.PayloadHash))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

            var mismatchIdsByKey = mismatchItems
                .GroupBy(m => (m.CashRegisterId, m.Canonical))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

            foreach (var kv in mismatchIdsByKey)
            {
                var key = kv.Key;
                var wantRepair = kv.Value;
                var k = wantRepair.Count;
                byRegHash.TryGetValue(key, out var alreadyCorrectIds);
                alreadyCorrectIds ??= new HashSet<Guid>();
                var externalOccupant = alreadyCorrectIds.Any(id => !wantRepair.Contains(id));
                if (externalOccupant)
                {
                    conflict += k;
                    continue;
                }

                // Same register+canonical: unique index allows only one row with this hash.
                repairable += 1;
                if (k > 1)
                    conflict += k - 1;
            }
        }

        var mismatch = mismatchItems.Count;
        var scanned = rows.Count;
        var ratioPercent = scanned > 0 ? 100.0 * mismatch / scanned : 0;
        var threshold = _guardOptions.MismatchWarningThresholdPercent;
        var riskHigh = ratioPercent >= threshold;
        var warningMessage = riskHigh
            ? $"Legacy payload_hash mismatch ratio {ratioPercent:F1}% exceeds threshold {threshold}%. Run repair or investigate before production. Scanned={scanned}, RuntimeMismatchCount={mismatch}."
            : null;

        if (riskHigh)
        {
            _logger.LogWarning(
                "Legacy payload_hash mismatch ratio high: {MismatchRatioPercent:F1}% (threshold {Threshold}%). Scanned={Scanned}, RuntimeMismatchCount={RuntimeMismatchCount}. Legacy data quality risk high.",
                ratioPercent, threshold, scanned, mismatch);
        }

        return new OfflinePayloadHashAnalyzeResult
        {
            Scanned = scanned,
            NullOrEmptyPayloadHash = nullHash,
            RuntimeMismatchCount = mismatch,
            RepairableNoConflictCount = repairable,
            SkippedWouldConflictCount = conflict,
            SampleMismatchIds = samples,
            MismatchRatioPercent = ratioPercent,
            LegacyDataQualityRiskHigh = riskHigh,
            WarningMessage = warningMessage
        };
    }

    public async Task<OfflinePayloadHashRepairResult> RepairAsync(
        int maxRows,
        bool dryRun,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        maxRows = Math.Clamp(maxRows, 1, 100_000);
        var query = _context.OfflineTransactions.AsQueryable();
        if (cashRegisterId.HasValue)
            query = query.Where(x => x.CashRegisterId == cashRegisterId.Value);

        var ids = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(maxRows)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var updated = 0;
        var skippedConflict = 0;
        var aligned = 0;
        var skippedNull = 0;
        var normErr = 0;

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = await _context.OfflineTransactions
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                .ConfigureAwait(false);
            if (row == null)
                continue;

            if (string.IsNullOrWhiteSpace(row.PayloadJson))
            {
                skippedNull++;
                continue;
            }

            string canonical;
            try
            {
                canonical = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(row.PayloadJson);
            }
            catch
            {
                normErr++;
                continue;
            }

            if (string.Equals(row.PayloadHash, canonical, StringComparison.OrdinalIgnoreCase))
            {
                aligned++;
                continue;
            }

            var hasConflict = await _context.OfflineTransactions.AnyAsync(
                x => x.CashRegisterId == row.CashRegisterId &&
                     x.PayloadHash == canonical &&
                     x.Id != row.Id,
                cancellationToken).ConfigureAwait(false);

            if (hasConflict)
            {
                skippedConflict++;
                continue;
            }

            if (!dryRun)
            {
                row.PayloadHash = canonical;
                row.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _metrics?.RecordPayloadHashMismatch(1);
                _logger.LogInformation(
                    "Offline payload_hash repaired for {OfflineId} (register {RegisterId})",
                    row.Id,
                    row.CashRegisterId);
            }

            updated++;
        }

        return new OfflinePayloadHashRepairResult
        {
            Scanned = ids.Count,
            Updated = updated,
            SkippedConflict = skippedConflict,
            SkippedAlreadyAligned = aligned,
            SkippedNullPayload = skippedNull,
            SkippedNormalizeError = normErr,
            DryRun = dryRun
        };
    }
}
