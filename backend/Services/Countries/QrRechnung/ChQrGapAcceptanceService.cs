using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Stores <see cref="SettingsKey"/> on <c>tenant_settings</c> for one mandant.
/// A null <c>tenant_id</c> row is ignored. There is no deployment-wide acceptance.
/// </summary>
public sealed class ChQrGapAcceptanceService : IChQrGapAcceptanceService
{
    public const string SettingsKey = "Fiscal.ChQrKnownGapsAccepted";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;
    private readonly ChQrKnownGapCatalog _catalog;
    private readonly IAuditLogService _audit;
    private readonly IActivityEventPublisher? _activity;
    private readonly ILogger<ChQrGapAcceptanceService> _logger;

    public ChQrGapAcceptanceService(
        AppDbContext db,
        ChQrKnownGapCatalog catalog,
        IAuditLogService audit,
        ILogger<ChQrGapAcceptanceService> logger,
        IActivityEventPublisher? activity = null)
    {
        _db = db;
        _catalog = catalog;
        _audit = audit;
        _logger = logger;
        _activity = activity;
    }

    public IReadOnlyList<ChQrKnownGap> KnownGaps => _catalog.Gaps;

    public async Task<ChQrGapAcceptance?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return null;

        var row = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(
                setting => setting.Key == SettingsKey && setting.TenantId == tenantId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
            return null;

        try
        {
            var stored = JsonSerializer.Deserialize<StoredAcceptance>(row.Value, JsonOptions);
            if (stored is null)
                return null;
            return new ChQrGapAcceptance(
                stored.AcceptedGaps ?? [],
                stored.AcceptedBy ?? string.Empty,
                DateTime.SpecifyKind(stored.AcceptedAtUtc, DateTimeKind.Utc));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "CH QR gap acceptance for tenant {TenantId} could not be read. Open gaps will warn. The invoice is not blocked.",
                tenantId);
            return null;
        }
    }

    public async Task<ChQrGapAcceptance> AcceptAsync(
        Guid tenantId,
        IReadOnlyList<string> acceptedGaps,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("Actor user id is required.", nameof(actorUserId));

        var normalized = Normalize(acceptedGaps);
        var invalid = normalized
            .Where(id => !_catalog.Ids.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (invalid.Length > 0)
            throw new ChQrUnknownGapException(invalid);

        var acceptedAt = DateTime.UtcNow;
        var actor = actorUserId.Trim();
        var payload = new StoredAcceptance
        {
            AcceptedGaps = normalized.ToList(),
            AcceptedBy = actor,
            AcceptedAtUtc = acceptedAt,
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
                UpdatedAtUtc = acceptedAt,
                UpdatedByUserId = actor,
            };
            _db.TenantSettings.Add(row);
        }
        else
        {
            row.Value = json;
            row.UpdatedAtUtc = acceptedAt;
            row.UpdatedByUserId = actor;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var acceptance = new ChQrGapAcceptance(normalized, actor, acceptedAt);
        await _audit.LogSystemOperationAsync(
            action: "CH_QR_KNOWN_GAPS_ACCEPTED",
            entityType: "TenantSetting",
            userId: actor,
            userRole: "SuperAdmin",
            description: "Operator acknowledged open CH QR print gaps. This is not a compliance claim.",
            actionType: AuditEventType.ChQrKnownGapsAccepted,
            entityId: row.Id,
            tenantId: tenantId,
            newValues: new
            {
                acceptedGaps = acceptance.AcceptedGaps,
                acceptedBy = acceptance.AcceptedBy,
                acceptedAtUtc = acceptance.AcceptedAtUtc,
            }).ConfigureAwait(false);

        if (_activity is not null)
        {
            await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.ChQrKnownGapsAccepted,
                metadata: new
                {
                    acceptedGaps = acceptance.AcceptedGaps,
                    acceptedBy = acceptance.AcceptedBy,
                    acceptedAtUtc = acceptance.AcceptedAtUtc,
                },
                actorUserId: actor,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return acceptance;
    }

    public async Task WarnOutstandingAsync(
        Guid tenantId,
        Guid? invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return;

        try
        {
            var acceptance = await GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var accepted = acceptance?.AcceptedGaps ?? [];
            var outstanding = _catalog.Gaps
                .Where(gap => !gap.Present)
                .Select(gap => gap.Id)
                .Where(id => !accepted.Contains(id, StringComparer.Ordinal))
                .ToArray();
            if (outstanding.Length == 0)
                return;

            _logger.LogWarning(
                "CH QR print gaps are not accepted for tenant {TenantId}: {GapIds}. Invoice {InvoiceId} was not blocked. This is not a compliance determination.",
                tenantId,
                string.Join(",", outstanding),
                invoiceId);

            if (_activity is null)
                return;

            await _activity.TryPublishAsync(
                tenantId,
                ActivityEventType.ChQrKnownGapsOutstanding,
                metadata: new
                {
                    outstandingGaps = outstanding,
                    invoiceId,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "CH QR gap warning failed for tenant {TenantId}. The invoice was not blocked.",
                tenantId);
        }
    }

    private static string[] Normalize(IReadOnlyList<string>? acceptedGaps)
    {
        if (acceptedGaps is null)
            throw new ArgumentException("Accepted gaps are required.", nameof(acceptedGaps));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>();
        foreach (var raw in acceptedGaps)
        {
            var id = raw?.Trim() ?? string.Empty;
            if (id.Length == 0)
                throw new ChQrUnknownGapException([raw ?? string.Empty]);
            if (seen.Add(id))
                normalized.Add(id);
        }

        return normalized.ToArray();
    }

    private sealed class StoredAcceptance
    {
        public List<string> AcceptedGaps { get; set; } = [];

        public string AcceptedBy { get; set; } = string.Empty;

        public DateTime AcceptedAtUtc { get; set; }
    }
}
