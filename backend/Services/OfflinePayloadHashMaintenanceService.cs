using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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

/// <summary>Read-only conflict group for triage; no auto-fix.</summary>
public sealed class PayloadHashConflictGroup
{
    public Guid CashRegisterId { get; init; }
    public string CanonicalHash { get; init; } = string.Empty;
    /// <summary>Row IDs that cannot be repaired (skipped).</summary>
    public IReadOnlyList<Guid> MismatchRowIds { get; init; } = Array.Empty<Guid>();
    /// <summary>Row IDs that already have this hash and block repair (when SkipReason is OccupantExists).</summary>
    public IReadOnlyList<Guid> OccupantRowIds { get; init; } = Array.Empty<Guid>();
    /// <summary>Why repair was skipped: OccupantExists | MultipleRowsForSameSlot.</summary>
    public string SkipReason { get; init; } = string.Empty;
    public DateTime? LatestCreatedAtUtc { get; init; }
    /// <summary>Suggested priority for triage: High = real occupant block, Medium = multiple rows for same slot.</summary>
    public string SeveritySuggestion { get; init; } = string.Empty;
}

/// <summary>Single row that can be repaired without conflict.</summary>
public sealed class PayloadHashRepairableItem
{
    public Guid CashRegisterId { get; init; }
    public string CanonicalHash { get; init; } = string.Empty;
    public Guid RowId { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
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

    /// <summary>Conflict groups for triage (read-only); grouped by (CashRegisterId, CanonicalHash).</summary>
    public IReadOnlyList<PayloadHashConflictGroup> ConflictGroups { get; init; } = Array.Empty<PayloadHashConflictGroup>();

    /// <summary>Rows that can be repaired with no conflict (one per (Register, Canonical) slot).</summary>
    public IReadOnlyList<PayloadHashRepairableItem> RepairableItems { get; init; } = Array.Empty<PayloadHashRepairableItem>();
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
    private readonly IDataProtector? _offlinePayloadProtector;
    private readonly IOptionsMonitor<OfflineVoucherEncryptionOptions>? _offlineVoucherEncryption;

    public OfflinePayloadHashMaintenanceService(
        AppDbContext context,
        ILogger<OfflinePayloadHashMaintenanceService> logger,
        IOptionsMonitor<PayloadHashGuardOptions>? guardOptions = null,
        ICoreMetrics? metrics = null,
        IDataProtectionProvider? dataProtectionProvider = null,
        IOptionsMonitor<OfflineVoucherEncryptionOptions>? offlineVoucherEncryption = null)
    {
        _context = context;
        _logger = logger;
        _guardOptions = guardOptions?.CurrentValue ?? new PayloadHashGuardOptions();
        _metrics = metrics;
        _offlinePayloadProtector = dataProtectionProvider != null
            ? OfflineVoucherPayloadProtector.CreateProtector(dataProtectionProvider)
            : null;
        _offlineVoucherEncryption = offlineVoucherEncryption;
    }

    private byte[]? GetOfflineVoucherFieldAesKeyBytes() =>
        OfflineVoucherEncryptionOptions.TryResolveKeyBytes(_offlineVoucherEncryption?.CurrentValue);

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
            .Select(x => new { x.Id, x.CashRegisterId, x.PayloadJson, x.PayloadSecretsProtected, x.PayloadHash, x.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nullHash = 0;
        var mismatchItems = new List<(Guid Id, Guid CashRegisterId, string Canonical, DateTime CreatedAt)>();
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
                var fullJson = OfflineVoucherPayloadProtector.ResolveNormalizedPayloadJson(
                    r.PayloadJson ?? "{}",
                    r.PayloadSecretsProtected,
                    _offlinePayloadProtector,
                    GetOfflineVoucherFieldAesKeyBytes());
                canonical = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(fullJson);
            }
            catch
            {
                continue;
            }

            if (string.Equals(r.PayloadHash.Trim(), canonical, StringComparison.OrdinalIgnoreCase))
                continue;

            mismatchItems.Add((r.Id, r.CashRegisterId, canonical, r.CreatedAt));
            if (samples.Count < 20)
                samples.Add(r.Id);
        }

        var repairable = 0;
        var conflict = 0;
        var conflictGroups = new List<PayloadHashConflictGroup>();
        var repairableItems = new List<PayloadHashRepairableItem>();

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
                .GroupBy(o => (o.CashRegisterId, Hash: o.PayloadHash!))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

            var mismatchByKey = mismatchItems
                .GroupBy(m => (m.CashRegisterId, m.Canonical))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kv in mismatchByKey)
            {
                var key = kv.Key;
                var list = kv.Value;
                var wantRepair = list.Select(x => x.Id).ToHashSet();
                var k = list.Count;
                var latestCreated = list.Max(x => x.CreatedAt);

                byRegHash.TryGetValue(key, out var alreadyCorrectIds);
                alreadyCorrectIds ??= new HashSet<Guid>();
                var externalOccupantIds = alreadyCorrectIds.Where(id => !wantRepair.Contains(id)).ToList();

                if (externalOccupantIds.Count > 0)
                {
                    conflict += k;
                    conflictGroups.Add(new PayloadHashConflictGroup
                    {
                        CashRegisterId = key.CashRegisterId,
                        CanonicalHash = key.Canonical,
                        MismatchRowIds = list.Select(x => x.Id).ToList(),
                        OccupantRowIds = externalOccupantIds,
                        SkipReason = "OccupantExists",
                        LatestCreatedAtUtc = latestCreated,
                        SeveritySuggestion = "High"
                    });
                    continue;
                }

                // No external occupant: one can be repaired, (k-1) conflict (multiple rows for same slot).
                var ordered = list.OrderBy(x => x.CreatedAt).ToList();
                repairableItems.Add(new PayloadHashRepairableItem
                {
                    CashRegisterId = key.CashRegisterId,
                    CanonicalHash = key.Canonical,
                    RowId = ordered[0].Id,
                    CreatedAtUtc = ordered[0].CreatedAt
                });
                repairable += 1;

                if (k > 1)
                {
                    conflict += k - 1;
                    conflictGroups.Add(new PayloadHashConflictGroup
                    {
                        CashRegisterId = key.CashRegisterId,
                        CanonicalHash = key.Canonical,
                        MismatchRowIds = ordered.Skip(1).Select(x => x.Id).ToList(),
                        OccupantRowIds = Array.Empty<Guid>(),
                        SkipReason = "MultipleRowsForSameSlot",
                        LatestCreatedAtUtc = latestCreated,
                        SeveritySuggestion = "Medium"
                    });
                }
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
            WarningMessage = warningMessage,
            ConflictGroups = conflictGroups,
            RepairableItems = repairableItems
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
                var fullJson = OfflineVoucherPayloadProtector.ResolveNormalizedPayloadJson(
                    row.PayloadJson,
                    row.PayloadSecretsProtected,
                    _offlinePayloadProtector,
                    GetOfflineVoucherFieldAesKeyBytes());
                canonical = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(fullJson);
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
