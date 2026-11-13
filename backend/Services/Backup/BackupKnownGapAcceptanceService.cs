using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Stores <see cref="SettingsKey"/> on <c>tenant_settings</c> for one mandant.
/// A null <c>tenant_id</c> row is ignored. There is no deployment-wide acceptance.
/// </summary>
public sealed class BackupKnownGapAcceptanceService : IBackupKnownGapAcceptanceService
{
    public const string SettingsKey = "Backup:KnownGapsAccepted";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;
    private readonly IBackupKnownGapsCatalog _catalog;
    private readonly IAuditLogService _audit;
    private readonly ILogger<BackupKnownGapAcceptanceService> _logger;

    public BackupKnownGapAcceptanceService(
        AppDbContext db,
        IBackupKnownGapsCatalog catalog,
        IAuditLogService audit,
        ILogger<BackupKnownGapAcceptanceService> logger)
    {
        _db = db;
        _catalog = catalog;
        _audit = audit;
        _logger = logger;
    }

    public async Task<BackupKnownGapAcceptanceDto> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Empty();

        var row = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(
                setting => setting.Key == SettingsKey && setting.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return Empty();

        try
        {
            var stored = JsonSerializer.Deserialize<StoredAcceptance>(row.Value, JsonOptions);
            if (stored is null)
                return Empty();
            return new BackupKnownGapAcceptanceDto
            {
                AcceptedGaps = stored.AcceptedGaps ?? [],
                AcceptedBy = stored.AcceptedBy ?? string.Empty,
                AcceptedAtUtc = stored.AcceptedAtUtc == default
                    ? null
                    : stored.AcceptedAtUtc.ToUniversalTime(),
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Backup known-gap acceptance for tenant {TenantId} could not be read. Open gaps stay unaccepted.",
                tenantId);
            return Empty();
        }
    }

    public async Task<BackupKnownGapAcceptanceDto> SaveAsync(
        Guid tenantId,
        IReadOnlyList<string> acceptedGapIds,
        string acceptedBy,
        DateTimeOffset acceptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var actor = (acceptedBy ?? string.Empty).Trim();
        if (actor.Length == 0)
            throw new ArgumentException("Accepted-by is required.", nameof(acceptedBy));
        if (actor.Length > 200)
            throw new ArgumentException("Accepted-by is too long.", nameof(acceptedBy));

        var normalized = Normalize(acceptedGapIds);
        var invalid = normalized
            .Where(id => !_catalog.Ids.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (invalid.Length > 0)
            throw new BackupKnownGapUnknownException(invalid);

        var stampedAt = acceptedAtUtc == default
            ? DateTimeOffset.UtcNow
            : acceptedAtUtc.ToUniversalTime();
        var payload = new StoredAcceptance
        {
            AcceptedGaps = normalized.ToList(),
            AcceptedBy = actor,
            AcceptedAtUtc = stampedAt,
        };
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var row = await _db.TenantSettings
            .FirstOrDefaultAsync(
                setting => setting.Key == SettingsKey && setting.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            row = new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = SettingsKey,
                Value = json,
                UpdatedAtUtc = stampedAt.UtcDateTime,
                UpdatedByUserId = actor,
            };
            _db.TenantSettings.Add(row);
        }
        else
        {
            row.Value = json;
            row.UpdatedAtUtc = stampedAt.UtcDateTime;
            row.UpdatedByUserId = actor;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var acceptance = new BackupKnownGapAcceptanceDto
        {
            AcceptedGaps = normalized,
            AcceptedBy = actor,
            AcceptedAtUtc = stampedAt,
        };

        await _audit.LogSystemOperationAsync(
            action: "BACKUP_KNOWN_GAPS_ACCEPTED",
            entityType: "TenantSetting",
            userId: actor,
            userRole: "SuperAdmin",
            description: "Operator acknowledged Backup/DR known gaps. This does not disable the checks and does not pass the compliance gate.",
            actionType: AuditEventType.BackupKnownGapsAccepted,
            entityId: row.Id,
            tenantId: tenantId,
            newValues: new
            {
                acceptedGaps = acceptance.AcceptedGaps,
                acceptedBy = acceptance.AcceptedBy,
                acceptedAtUtc = acceptance.AcceptedAtUtc,
            }).ConfigureAwait(false);

        _logger.LogInformation(
            "Backup known gaps accepted for tenant {TenantId} by {AcceptedBy}: {GapIds}.",
            tenantId,
            actor,
            string.Join(",", acceptance.AcceptedGaps));

        return acceptance;
    }

    private static BackupKnownGapAcceptanceDto Empty() => new();

    private static string[] Normalize(IReadOnlyList<string>? acceptedGapIds)
    {
        if (acceptedGapIds is null)
            throw new ArgumentException("Accepted gap ids are required.", nameof(acceptedGapIds));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>();
        foreach (var raw in acceptedGapIds)
        {
            var id = raw?.Trim() ?? string.Empty;
            if (id.Length == 0)
                throw new BackupKnownGapUnknownException([raw ?? string.Empty]);
            if (seen.Add(id))
                normalized.Add(id);
        }

        return normalized.ToArray();
    }

    private sealed class StoredAcceptance
    {
        public List<string> AcceptedGaps { get; set; } = [];

        public string AcceptedBy { get; set; } = string.Empty;

        public DateTimeOffset AcceptedAtUtc { get; set; }
    }
}
