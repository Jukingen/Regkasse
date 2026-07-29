using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

public interface IRksvAusfallEpisodeService
{
    Task<IReadOnlyList<RksvAusfallEpisodeDto>> ListAsync(
        Guid? tenantId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<RksvAusfallTriggerResponse> TriggerAsync(
        RksvAusfallTriggerRequest request,
        Guid tenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    Task<RksvAusfallTriggerResponse> ApproveAndEnqueueAsync(
        Guid episodeId,
        Guid? scopeTenantId,
        string actorUserId,
        string actorRole,
        string? operatorNote,
        CancellationToken cancellationToken = default);

    Task<RksvAusfallTriggerResponse> MarkManualPortalAsync(
        Guid episodeId,
        Guid? scopeTenantId,
        string actorUserId,
        string actorRole,
        RksvAusfallMarkManualRequest request,
        CancellationToken cancellationToken = default);

    Task<RksvAusfallTriggerResponse> CancelSuggestionAsync(
        Guid episodeId,
        Guid? scopeTenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>Failover activated → Ausfall SE suggestion (or auto-enqueue).</summary>
    Task SuggestAusfallFromFailoverAsync(TseDevice primary, CancellationToken cancellationToken = default);

    /// <summary>Primary recovered → Wiederinbetriebnahme SE suggestion (or auto-enqueue).</summary>
    Task SuggestWiederinbetriebnahmeFromRevertAsync(TseDevice primary, CancellationToken cancellationToken = default);
}

public sealed class RksvAusfallEpisodeService : IRksvAusfallEpisodeService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly AppDbContext _db;
    private readonly IFinanzOnlineOutboxService _outbox;
    private readonly IOptionsMonitor<AusfallOptions> _ausfallOptions;
    private readonly IOptionsMonitor<FinanzOnlineModeOptions> _modeOptions;
    private readonly IOptionsMonitor<FinanzOnlineCutoverGuardOptions> _cutoverOptions;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IAuditLogService _audit;
    private readonly IActivityEventPublisher _activity;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<RksvAusfallEpisodeService> _logger;

    public RksvAusfallEpisodeService(
        AppDbContext db,
        IFinanzOnlineOutboxService outbox,
        IOptionsMonitor<AusfallOptions> ausfallOptions,
        IOptionsMonitor<FinanzOnlineModeOptions> modeOptions,
        IOptionsMonitor<FinanzOnlineCutoverGuardOptions> cutoverOptions,
        IOptionsMonitor<TseOptions> tseOptions,
        IAuditLogService audit,
        IActivityEventPublisher activity,
        IFeatureFlagService featureFlags,
        ILogger<RksvAusfallEpisodeService> logger)
    {
        _db = db;
        _outbox = outbox;
        _ausfallOptions = ausfallOptions;
        _modeOptions = modeOptions;
        _cutoverOptions = cutoverOptions;
        _tseOptions = tseOptions;
        _audit = audit;
        _activity = activity;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RksvAusfallEpisodeDto>> ListAsync(
        Guid? tenantId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var q = _db.RksvAusfallEpisodes.AsNoTracking().AsQueryable();
        if (tenantId is { } tid)
            q = q.Where(e => e.TenantId == tid);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(e => e.Status == status.Trim());

        var rows = await q.OrderByDescending(e => e.CreatedAtUtc).Take(200).ToListAsync(cancellationToken).ConfigureAwait(false);
        var deviceIds = rows.Where(r => r.DeviceId.HasValue).Select(r => r.DeviceId!.Value).Distinct().ToList();
        var devices = deviceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.TseDevices.AsNoTracking()
                .Where(d => deviceIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.SerialNumber, cancellationToken)
                .ConfigureAwait(false);

        return rows.Select(e => ToDto(e, e.DeviceId is { } id && devices.TryGetValue(id, out var s) ? s : null)).ToList();
    }

    public async Task<RksvAusfallTriggerResponse> TriggerAsync(
        RksvAusfallTriggerRequest request,
        Guid tenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (IsDemoOrSoftEnvironment())
            return Fail("AUSFALL_DEMO_SOFT_SKIP", "Ausfall FON reporting is disabled for Demo/Soft/Off TSE modes.");

        var episodeType = NormalizeEpisodeType(request.EpisodeType);
        var operation = NormalizeOperation(request.OperationKind);
        var begruendung = string.IsNullOrWhiteSpace(request.Begruendung)
            ? RksvAusfallBegruendungCodes.Other
            : request.Begruendung.Trim();
        if (!RksvAusfallBegruendungCodes.IsKnown(begruendung))
            begruendung = RksvAusfallBegruendungCodes.Other;

        string? cert = request.CertificateSerial?.Trim();
        string? kassen = request.KassenId?.Trim();
        Guid? deviceId = request.DeviceId;
        Guid? cashRegisterId = request.CashRegisterId;

        if (deviceId is { } did)
        {
            var device = await _db.TseDevices.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == did, cancellationToken)
                .ConfigureAwait(false);
            if (device == null || (device.TenantId.HasValue && device.TenantId != tenantId))
                return Fail("AUSFALL_DEVICE_NOT_FOUND", "TSE device not found for tenant.");
            cert ??= string.IsNullOrWhiteSpace(device.SerialNumber) ? null : device.SerialNumber.Trim();
            cashRegisterId ??= device.CashRegisterId;
            if (string.IsNullOrWhiteSpace(kassen) && device.KassenId != Guid.Empty)
                kassen = device.KassenId.ToString("N");
        }

        if (string.Equals(episodeType, RksvAusfallEpisodeTypes.Scu, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(cert))
            return Fail("AUSFALL_CERT_REQUIRED", "Certificate serial is required for SCU Ausfall.");

        if (string.Equals(episodeType, RksvAusfallEpisodeTypes.Kasse, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(kassen))
            return Fail("AUSFALL_KASSEN_REQUIRED", "Kassen-ID is required for Kasse Ausfall.");

        var isWieder = string.Equals(operation, RksvAusfallOperationKinds.Wiederinbetriebnahme, StringComparison.Ordinal);
        var beginn = request.BeginnUtc?.ToUniversalTime() ?? (isWieder ? null : DateTimeOffset.UtcNow);
        var ende = request.EndeUtc?.ToUniversalTime() ?? (isWieder ? DateTimeOffset.UtcNow : null);

        var episode = new RksvAusfallEpisode
        {
            TenantId = tenantId,
            DeviceId = deviceId,
            EpisodeType = episodeType,
            OperationKind = operation,
            Begruendung = begruendung,
            BeginnUtc = beginn,
            EndeUtc = ende,
            CertificateSerial = cert,
            KassenId = kassen,
            CashRegisterId = cashRegisterId,
            RelatedAusfallEpisodeId = request.RelatedAusfallEpisodeId,
            OperatorNote = Truncate(request.OperatorNote, 500),
            CreatedBy = Truncate(actorUserId, 128),
            Status = request.EnqueueImmediately
                ? RksvAusfallEpisodeStatuses.PendingApproval
                : RksvAusfallEpisodeStatuses.Suggested,
        };

        _db.RksvAusfallEpisodes.Add(episode);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(
            AuditEventType.RksvAusfallEpisodeCreated,
            tenantId,
            actorUserId,
            actorRole,
            episode,
            cancellationToken).ConfigureAwait(false);

        await PublishActivityAsync(
            tenantId,
            isWieder ? ActivityEventType.TseWiederinbetriebnahmeReported : ActivityEventType.TseAusfallEnqueueSuggested,
            episode,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        if (request.EnqueueImmediately)
        {
            var enq = await EnqueueAsync(episode, actorUserId, actorRole, cancellationToken).ConfigureAwait(false);
            if (!enq.Success)
                return enq;
        }

        return new RksvAusfallTriggerResponse { Success = true, Episode = await LoadDtoAsync(episode.Id, cancellationToken).ConfigureAwait(false) };
    }

    public async Task<RksvAusfallTriggerResponse> ApproveAndEnqueueAsync(
        Guid episodeId,
        Guid? scopeTenantId,
        string actorUserId,
        string actorRole,
        string? operatorNote,
        CancellationToken cancellationToken = default)
    {
        if (IsDemoOrSoftEnvironment())
            return Fail("AUSFALL_DEMO_SOFT_SKIP", "Ausfall FON reporting is disabled for Demo/Soft/Off TSE modes.");

        var episode = await FindScopedAsync(episodeId, scopeTenantId, cancellationToken).ConfigureAwait(false);
        if (episode == null)
            return Fail("AUSFALL_NOT_FOUND", "Episode not found.");

        if (episode.Status is not (RksvAusfallEpisodeStatuses.Suggested or RksvAusfallEpisodeStatuses.PendingApproval or RksvAusfallEpisodeStatuses.Failed))
            return Fail("AUSFALL_INVALID_STATUS", $"Cannot approve episode in status {episode.Status}.");

        if (!string.IsNullOrWhiteSpace(operatorNote))
            episode.OperatorNote = Truncate(operatorNote, 500);

        return await EnqueueAsync(episode, actorUserId, actorRole, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RksvAusfallTriggerResponse> MarkManualPortalAsync(
        Guid episodeId,
        Guid? scopeTenantId,
        string actorUserId,
        string actorRole,
        RksvAusfallMarkManualRequest request,
        CancellationToken cancellationToken = default)
    {
        var episode = await FindScopedAsync(episodeId, scopeTenantId, cancellationToken).ConfigureAwait(false);
        if (episode == null)
            return Fail("AUSFALL_NOT_FOUND", "Episode not found.");

        episode.Status = RksvAusfallEpisodeStatuses.Closed;
        episode.ExternalReference = Truncate(request.ExternalReference ?? "PORTAL_MANUAL", 120);
        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
            episode.OperatorNote = Truncate(request.OperatorNote, 500);
        episode.ApprovedBy = Truncate(actorUserId, 128);
        episode.ApprovedAtUtc = DateTimeOffset.UtcNow;
        episode.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(
            AuditEventType.RksvAusfallMarkedManualPortal,
            episode.TenantId,
            actorUserId,
            actorRole,
            episode,
            cancellationToken).ConfigureAwait(false);

        return new RksvAusfallTriggerResponse
        {
            Success = true,
            Episode = await LoadDtoAsync(episode.Id, cancellationToken).ConfigureAwait(false),
        };
    }

    public async Task<RksvAusfallTriggerResponse> CancelSuggestionAsync(
        Guid episodeId,
        Guid? scopeTenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var episode = await FindScopedAsync(episodeId, scopeTenantId, cancellationToken).ConfigureAwait(false);
        if (episode == null)
            return Fail("AUSFALL_NOT_FOUND", "Episode not found.");

        if (episode.Status is not (RksvAusfallEpisodeStatuses.Suggested or RksvAusfallEpisodeStatuses.PendingApproval))
            return Fail("AUSFALL_INVALID_STATUS", "Only Suggested/PendingApproval episodes can be cancelled.");

        episode.Status = RksvAusfallEpisodeStatuses.Closed;
        episode.UpdatedAtUtc = DateTimeOffset.UtcNow;
        episode.OperatorNote = Truncate($"{episode.OperatorNote}; cancelled by {actorUserId}".Trim(';', ' '), 500);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(
            AuditEventType.RksvAusfallSuggestionCancelled,
            episode.TenantId,
            actorUserId,
            actorRole,
            episode,
            cancellationToken).ConfigureAwait(false);

        return new RksvAusfallTriggerResponse
        {
            Success = true,
            Episode = await LoadDtoAsync(episode.Id, cancellationToken).ConfigureAwait(false),
        };
    }

    public async Task SuggestAusfallFromFailoverAsync(TseDevice primary, CancellationToken cancellationToken = default)
    {
        if (IsDemoOrSoftEnvironment())
        {
            _logger.LogInformation("Ausfall suggestion skipped (Demo/Soft/Off). DeviceId={DeviceId}", primary.Id);
            return;
        }

        if (primary.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            _logger.LogWarning("Ausfall suggestion skipped (no tenant). DeviceId={DeviceId}", primary.Id);
            return;
        }

        var auto = _ausfallOptions.CurrentValue.AutoEnqueue
            && _featureFlags.IsEnabled(FeatureFlagNames.EnableAutoAusfall, tenantId.ToString("D"));
        var response = await TriggerAsync(
            new RksvAusfallTriggerRequest
            {
                DeviceId = primary.Id,
                EpisodeType = RksvAusfallEpisodeTypes.Scu,
                OperationKind = RksvAusfallOperationKinds.Ausfall,
                Begruendung = RksvAusfallBegruendungCodes.HardwareDefect,
                BeginnUtc = DateTimeOffset.UtcNow,
                CertificateSerial = primary.SerialNumber,
                OperatorNote = "Suggested from TSE failover activation.",
                EnqueueImmediately = auto,
            },
            tenantId,
            TseFailoverService.SystemActorUserId,
            "System",
            cancellationToken).ConfigureAwait(false);

        if (!response.Success)
            _logger.LogWarning("Ausfall suggestion failed: {Code} {Message}", response.ErrorCode, response.Message);
    }

    public async Task SuggestWiederinbetriebnahmeFromRevertAsync(TseDevice primary, CancellationToken cancellationToken = default)
    {
        if (IsDemoOrSoftEnvironment())
        {
            _logger.LogInformation("Wiederinbetriebnahme suggestion skipped (Demo/Soft/Off). DeviceId={DeviceId}", primary.Id);
            return;
        }

        if (primary.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            _logger.LogWarning("Wiederinbetriebnahme suggestion skipped (no tenant). DeviceId={DeviceId}", primary.Id);
            return;
        }

        var openAusfall = await _db.RksvAusfallEpisodes
            .Where(e =>
                e.TenantId == tenantId &&
                e.DeviceId == primary.Id &&
                e.OperationKind == RksvAusfallOperationKinds.Ausfall &&
                (e.Status == RksvAusfallEpisodeStatuses.Suggested ||
                 e.Status == RksvAusfallEpisodeStatuses.PendingApproval ||
                 e.Status == RksvAusfallEpisodeStatuses.Submitted ||
                 e.Status == RksvAusfallEpisodeStatuses.Verified))
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var auto = _ausfallOptions.CurrentValue.AutoEnqueue
            && _featureFlags.IsEnabled(FeatureFlagNames.EnableAutoAusfall, tenantId.ToString("D"));
        var response = await TriggerAsync(
            new RksvAusfallTriggerRequest
            {
                DeviceId = primary.Id,
                EpisodeType = RksvAusfallEpisodeTypes.Scu,
                OperationKind = RksvAusfallOperationKinds.Wiederinbetriebnahme,
                Begruendung = openAusfall?.Begruendung ?? RksvAusfallBegruendungCodes.Other,
                EndeUtc = DateTimeOffset.UtcNow,
                CertificateSerial = primary.SerialNumber,
                RelatedAusfallEpisodeId = openAusfall?.Id,
                OperatorNote = "Suggested from TSE failover revert to primary.",
                EnqueueImmediately = auto,
            },
            tenantId,
            TseFailoverService.SystemActorUserId,
            "System",
            cancellationToken).ConfigureAwait(false);

        if (!response.Success)
            _logger.LogWarning("Wiederinbetriebnahme suggestion failed: {Code} {Message}", response.ErrorCode, response.Message);
    }

    private async Task<RksvAusfallTriggerResponse> EnqueueAsync(
        RksvAusfallEpisode episode,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        FinanzOnlineIntegrationMode mode;
        try
        {
            mode = FinanzOnlineModeResolver.ResolveOutboxMode(
                _modeOptions.CurrentValue.Mode,
                _cutoverOptions.CurrentValue,
                out _);
        }
        catch (InvalidOperationException)
        {
            mode = FinanzOnlineIntegrationMode.TEST;
        }

        var messageType = ResolveMessageType(episode);
        var inner = new RksvAusfallOutboxPayloadBody
        {
            EpisodeId = episode.Id,
            EpisodeType = episode.EpisodeType,
            OperationKind = episode.OperationKind,
            Begruendung = episode.Begruendung,
            BeginnUtc = episode.BeginnUtc,
            EndeUtc = episode.EndeUtc,
            CertificateSerial = episode.CertificateSerial,
            KassenId = episode.KassenId,
            DeviceId = episode.DeviceId,
            CashRegisterId = episode.CashRegisterId,
        };
        var innerJson = JsonSerializer.Serialize(inner, JsonOpts);
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(innerJson))).ToLowerInvariant();
        var beginnKey = (episode.BeginnUtc ?? episode.EndeUtc ?? episode.CreatedAtUtc).UtcDateTime.ToString("o");
        var idPart = episode.CertificateSerial ?? episode.KassenId ?? episode.Id.ToString("N");
        var businessKey = $"ausfall|{episode.TenantId:N}|{episode.EpisodeType}|{idPart}|{episode.OperationKind}|{beginnKey}";

        var outboxRow = await _outbox.EnqueueSubmissionAsync(
            aggregateType: "RksvAusfallEpisode",
            aggregateId: episode.Id,
            messageType: messageType,
            businessKey: businessKey,
            payload: new FinanzOnlineOutboxPayload
            {
                Mode = mode,
                Scope = new FinanzOnlineScope
                {
                    TenantId = episode.TenantId.ToString("N"),
                    RegisterId = episode.KassenId ?? episode.CertificateSerial ?? episode.Id.ToString("N"),
                },
                Correlation = new FinanzOnlineCorrelationContext
                {
                    BusinessKey = businessKey,
                    PayloadHash = payloadHash,
                    CorrelationId = episode.Id.ToString("N"),
                },
                SubmissionKind = string.Equals(episode.EpisodeType, RksvAusfallEpisodeTypes.Scu, StringComparison.Ordinal)
                    ? FinanzOnlineSubmissionKind.SignatureUnit
                    : FinanzOnlineSubmissionKind.Register,
                PayloadJson = innerJson,
            },
            cancellationToken,
            persistImmediately: true).ConfigureAwait(false);

        episode.OutboxMessageId = outboxRow.Id;
        episode.Status = RksvAusfallEpisodeStatuses.Submitted;
        episode.ApprovedBy = Truncate(actorUserId, 128);
        episode.ApprovedAtUtc = DateTimeOffset.UtcNow;
        episode.UpdatedAtUtc = DateTimeOffset.UtcNow;
        episode.LastErrorCode = null;
        episode.LastErrorMessage = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(
            AuditEventType.RksvAusfallEpisodeEnqueued,
            episode.TenantId,
            actorUserId,
            actorRole,
            episode,
            cancellationToken).ConfigureAwait(false);

        var isWieder = string.Equals(episode.OperationKind, RksvAusfallOperationKinds.Wiederinbetriebnahme, StringComparison.Ordinal);
        await PublishActivityAsync(
            episode.TenantId,
            isWieder ? ActivityEventType.TseWiederinbetriebnahmeReported : ActivityEventType.TseAusfallReported,
            episode,
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return new RksvAusfallTriggerResponse
        {
            Success = true,
            Episode = await LoadDtoAsync(episode.Id, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<RksvAusfallEpisode?> FindScopedAsync(Guid id, Guid? scopeTenantId, CancellationToken ct)
    {
        var q = _db.RksvAusfallEpisodes.Where(e => e.Id == id);
        if (scopeTenantId is { } tid)
            q = q.Where(e => e.TenantId == tid);
        return await q.FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    private async Task<RksvAusfallEpisodeDto?> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var e = await _db.RksvAusfallEpisodes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (e == null) return null;
        string? serial = null;
        if (e.DeviceId is { } did)
            serial = await _db.TseDevices.AsNoTracking().Where(d => d.Id == did).Select(d => d.SerialNumber).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return ToDto(e, serial);
    }

    private bool IsDemoOrSoftEnvironment()
    {
        var o = _tseOptions.CurrentValue;
        return o.IsOff || o.UseSoftTseWhenNoDevice ||
               string.Equals(o.TseMode, "Demo", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(o.TseMode, "Soft", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveMessageType(RksvAusfallEpisode episode)
    {
        var isScu = string.Equals(episode.EpisodeType, RksvAusfallEpisodeTypes.Scu, StringComparison.Ordinal);
        var isWieder = string.Equals(episode.OperationKind, RksvAusfallOperationKinds.Wiederinbetriebnahme, StringComparison.Ordinal);
        return (isScu, isWieder) switch
        {
            (true, false) => FinanzOnlineRksvAusfallOutboxMessageTypes.RksvAusfallSeSubmission,
            (true, true) => FinanzOnlineRksvAusfallOutboxMessageTypes.RksvWiederinbetriebnahmeSeSubmission,
            (false, false) => FinanzOnlineRksvAusfallOutboxMessageTypes.RksvAusfallKasseSubmission,
            _ => FinanzOnlineRksvAusfallOutboxMessageTypes.RksvWiederinbetriebnahmeKasseSubmission,
        };
    }

    private async Task WriteAuditAsync(
        AuditEventType type,
        Guid tenantId,
        string actorUserId,
        string actorRole,
        RksvAusfallEpisode episode,
        CancellationToken ct)
    {
        await _audit.LogSystemOperationAsync(
                action: type.ToString(),
                entityType: nameof(RksvAusfallEpisode),
                userId: actorUserId,
                userRole: actorRole,
                description: $"{episode.OperationKind} {episode.EpisodeType} status={episode.Status}",
                actionType: type,
                entityId: episode.Id,
                tenantId: tenantId,
                newValues: new
                {
                    episode.Status,
                    episode.OperationKind,
                    episode.EpisodeType,
                    episode.OutboxMessageId,
                    begruendung = episode.Begruendung,
                })
            .ConfigureAwait(false);
    }

    private async Task PublishActivityAsync(
        Guid tenantId,
        ActivityEventType type,
        RksvAusfallEpisode episode,
        string actorUserId,
        CancellationToken ct)
    {
        try
        {
            await _activity.TryPublishAsync(
                    tenantId,
                    type,
                    metadata: new
                    {
                        EpisodeId = episode.Id.ToString("D"),
                        Status = episode.Status,
                        OperationKind = episode.OperationKind,
                        EpisodeType = episode.EpisodeType,
                        OutboxMessageId = episode.OutboxMessageId?.ToString("D") ?? "",
                        Message = $"{episode.OperationKind} ({episode.EpisodeType}) — {episode.Status}",
                        deepLink = "/admin/tse/ausfall",
                    },
                    actorUserId: actorUserId,
                    dedupKey: $"ausfall:{episode.Id:N}:{type}",
                    cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ausfall activity publish failed EpisodeId={EpisodeId}", episode.Id);
        }
    }

    private static RksvAusfallEpisodeDto ToDto(RksvAusfallEpisode e, string? deviceSerial) => new()
    {
        Id = e.Id,
        TenantId = e.TenantId,
        DeviceId = e.DeviceId,
        DeviceSerial = deviceSerial,
        EpisodeType = e.EpisodeType,
        OperationKind = e.OperationKind,
        Begruendung = e.Begruendung,
        BeginnUtc = e.BeginnUtc,
        EndeUtc = e.EndeUtc,
        Status = e.Status,
        OutboxMessageId = e.OutboxMessageId,
        ExternalReference = e.ExternalReference,
        CertificateSerial = e.CertificateSerial,
        KassenId = e.KassenId,
        CashRegisterId = e.CashRegisterId,
        RelatedAusfallEpisodeId = e.RelatedAusfallEpisodeId,
        OperatorNote = e.OperatorNote,
        CreatedBy = e.CreatedBy,
        ApprovedBy = e.ApprovedBy,
        ApprovedAtUtc = e.ApprovedAtUtc,
        LastErrorCode = e.LastErrorCode,
        LastErrorMessage = e.LastErrorMessage,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc,
    };

    private static RksvAusfallTriggerResponse Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, Message = message };

    private static string NormalizeEpisodeType(string? v) =>
        string.Equals(v, RksvAusfallEpisodeTypes.Kasse, StringComparison.OrdinalIgnoreCase)
            ? RksvAusfallEpisodeTypes.Kasse
            : RksvAusfallEpisodeTypes.Scu;

    private static string NormalizeOperation(string? v) =>
        string.Equals(v, RksvAusfallOperationKinds.Wiederinbetriebnahme, StringComparison.OrdinalIgnoreCase)
            ? RksvAusfallOperationKinds.Wiederinbetriebnahme
            : RksvAusfallOperationKinds.Ausfall;

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}

public sealed class RksvAusfallOutboxPayloadBody
{
    public Guid EpisodeId { get; set; }
    public string EpisodeType { get; set; } = string.Empty;
    public string OperationKind { get; set; } = string.Empty;
    public string Begruendung { get; set; } = string.Empty;
    public DateTimeOffset? BeginnUtc { get; set; }
    public DateTimeOffset? EndeUtc { get; set; }
    public string? CertificateSerial { get; set; }
    public string? KassenId { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? CashRegisterId { get; set; }
}
