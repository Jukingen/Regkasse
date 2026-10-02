using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

public interface IPeppolSubmissionService
{
    Task<PeppolSubmission> SubmitAsync(
        Guid tenantId,
        InvoiceDocumentDto document,
        string? participantId = null,
        CancellationToken cancellationToken = default,
        Guid? invoiceId = null);

    Task<PeppolSubmission?> GetStatusAsync(string submissionId, CancellationToken cancellationToken = default);
}

public sealed class PeppolSubmissionService : IPeppolSubmissionService
{
    private readonly IEn16931XmlBuilder _xml;
    private readonly PeppolOptions _options;
    private readonly MockPeppolAccessPointClient _mock;
    private readonly HostedPeppolAccessPointClient _hosted;
    private readonly IPeppolSubmissionStore _store;
    private readonly IAuditLogService? _audit;
    private readonly IActivityEventPublisher? _activity;
    private readonly IFeatureFlagService? _featureFlags;
    private readonly AppDbContext? _db;

    public const string ReservedFailureReason = "peppol-reserved";
    public const string LiveNotAllowedReason = StorecovePeppolAccessPointClient.LiveNotAllowedCode;
    public const string ProviderNotStorecoveReason = "peppol-provider-not-storecove";

    private readonly StorecovePeppolAccessPointClient? _storecove;
    private readonly IPeppolAccessPointClient? _canaryTransport;
    private readonly ILogger<PeppolSubmissionService>? _logger;

    public PeppolSubmissionService(
        IEn16931XmlBuilder xml,
        IOptions<PeppolOptions> options,
        MockPeppolAccessPointClient mock,
        HostedPeppolAccessPointClient hosted,
        IPeppolSubmissionStore store,
        IAuditLogService? audit = null,
        IFeatureFlagService? featureFlags = null,
        AppDbContext? db = null,
        IActivityEventPublisher? activity = null,
        StorecovePeppolAccessPointClient? storecove = null,
        ILogger<PeppolSubmissionService>? logger = null,
        IPeppolAccessPointClient? canaryTransport = null)
    {
        _xml = xml;
        _options = options.Value;
        _mock = mock;
        _hosted = hosted;
        _store = store;
        _audit = audit;
        _featureFlags = featureFlags;
        _db = db;
        _activity = activity;
        _storecove = storecove;
        _logger = logger;
        // Production always has the Storecove client. Tests pass a mock here and leave that client null.
        _canaryTransport = canaryTransport;
    }

    public async Task<PeppolSubmission> SubmitAsync(
        Guid tenantId,
        InvoiceDocumentDto document,
        string? participantId = null,
        CancellationToken cancellationToken = default,
        Guid? invoiceId = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var invoiceNumber = document.InvoiceNumber ?? string.Empty;
        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931))
        {
            var heldOff = new PeppolSubmission(
                id, tenantId, invoiceNumber, PeppolSubmissionStatus.Validated, participantId, "not-sent");
            _store.Save(heldOff);
            return heldOff;
        }

        var xml = await _xml.BuildXmlAsync(document, cancellationToken).ConfigureAwait(false);
        var failures = En16931Schematron.Validate(xml);
        if (failures.Count > 0)
        {
            var failed = new PeppolSubmission(
                id, tenantId, invoiceNumber, PeppolSubmissionStatus.Failed, participantId,
                string.Join("; ", failures));
            _store.Save(failed);
            await AuditAsync("EINVOICE_SUBMISSION_FAILED", AuditEventType.EinvoiceSubmissionFailed, tenantId, id, failed.Detail, cancellationToken)
                .ConfigureAwait(false);
            return failed;
        }

        var provider = _options.Provider?.Trim() ?? "not-configured";
        if (FeatureFlagNames.Reserved.Contains(FeatureFlagNames.EInvoicingPeppol)
            && IsCanaryTenant(tenantId))
        {
            return await SubmitCanaryAsync(
                tenantId,
                invoiceNumber,
                participantId,
                xml,
                invoiceId,
                cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(provider, "not-configured", StringComparison.OrdinalIgnoreCase))
        {
            var held = new PeppolSubmission(
                id, tenantId, invoiceNumber, PeppolSubmissionStatus.Validated, participantId, "not-sent");
            _store.Save(held);
            await AuditAsync("EINVOICE_VALIDATED", AuditEventType.EinvoiceValidated, tenantId, id, "schematron-pass", cancellationToken)
                .ConfigureAwait(false);
            return held;
        }

        IPeppolAccessPointClient client = string.Equals(provider, "mock", StringComparison.OrdinalIgnoreCase)
            ? _mock
            : _hosted;

        // Everyone except the canary stays on the reserved queue. The flag stays in Reserved.
        if (FeatureFlagNames.Reserved.Contains(FeatureFlagNames.EInvoicingPeppol))
        {
            await QueueReservedAsync(tenantId, invoiceId, cancellationToken).ConfigureAwait(false);
            var queued = new PeppolSubmission(
                id, tenantId, invoiceNumber, PeppolSubmissionStatus.Queued, participantId, ReservedFailureReason);
            _store.Save(queued);
            return queued;
        }

        try
        {
            var transport = await client.SubmitAsync(
                new PeppolSubmitRequest(id, xml, participantId ?? string.Empty),
                cancellationToken).ConfigureAwait(false);
            var row = new PeppolSubmission(
                id, tenantId, invoiceNumber, transport.Status, participantId, transport.Detail);
            _store.Save(row);
            var auditType = transport.Status == PeppolSubmissionStatus.Failed
                ? AuditEventType.EinvoiceSubmissionFailed
                : AuditEventType.EinvoiceSubmitted;
            await AuditAsync(auditType.ToString(), auditType, tenantId, id, transport.Detail, cancellationToken)
                .ConfigureAwait(false);
            return row;
        }
        catch (PeppolAccessPointNotConfiguredException ex)
        {
            var failed = new PeppolSubmission(
                id, tenantId, invoiceNumber, PeppolSubmissionStatus.Failed, participantId, ex.Message);
            _store.Save(failed);
            await AuditAsync("EINVOICE_SUBMISSION_FAILED", AuditEventType.EinvoiceSubmissionFailed, tenantId, id, ex.Message, cancellationToken)
                .ConfigureAwait(false);
            return failed;
        }
    }

    public async Task<PeppolSubmission?> GetStatusAsync(
        string submissionId,
        CancellationToken cancellationToken = default)
    {
        var current = _store.Get(submissionId);
        if (current is null || current.Status is PeppolSubmissionStatus.Failed or PeppolSubmissionStatus.Validated or PeppolSubmissionStatus.Ack)
            return current;

        if (FeatureFlagNames.Reserved.Contains(FeatureFlagNames.EInvoicingPeppol))
            return current;

        if (_featureFlags is not null
            && !_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931))
            return current;

        var provider = _options.Provider?.Trim() ?? "not-configured";
        if (string.Equals(provider, "not-configured", StringComparison.OrdinalIgnoreCase))
            return current;

        IPeppolAccessPointClient client = string.Equals(provider, "mock", StringComparison.OrdinalIgnoreCase)
            ? _mock
            : _hosted;
        var transport = await client.GetStatusAsync(submissionId, cancellationToken).ConfigureAwait(false);
        var updated = current with { Status = transport.Status, Detail = transport.Detail };
        _store.Save(updated);
        return updated;
    }

    private bool IsCanaryTenant(Guid tenantId)
    {
        if (!_options.ReservedExit.Enabled || tenantId == Guid.Empty)
            return false;

        var raw = _options.ReservedExit.CanaryTenantId?.Trim();
        return Guid.TryParse(raw, out var canary) && canary == tenantId;
    }

    private async Task<PeppolSubmission> SubmitCanaryAsync(
        Guid tenantId,
        string invoiceNumber,
        string? participantId,
        string xml,
        Guid? invoiceId,
        CancellationToken cancellationToken)
    {
        var provider = _options.Provider?.Trim() ?? "not-configured";
        var storecove = ResolveCanaryClient(provider);
        if (storecove is null)
        {
            return await FinishCanaryAsync(
                tenantId,
                invoiceNumber,
                participantId,
                invoiceId,
                EinvoiceSubmissionStatuses.Failed,
                ProviderNotStorecoveReason,
                providerMessageId: null,
                providerStatus: null,
                attemptedAtUtc: null,
                cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(_options.Storecove.Environment?.Trim(), "TEST", StringComparison.Ordinal))
        {
            return await FinishCanaryAsync(
                tenantId,
                invoiceNumber,
                participantId,
                invoiceId,
                EinvoiceSubmissionStatuses.Failed,
                LiveNotAllowedReason,
                providerMessageId: null,
                providerStatus: null,
                attemptedAtUtc: null,
                cancellationToken).ConfigureAwait(false);
        }

        var submissionId = Guid.NewGuid();
        var attemptedAt = DateTime.UtcNow;
        try
        {
            var transport = await storecove.SubmitAsync(
                new PeppolSubmitRequest(submissionId.ToString("D"), xml, participantId ?? string.Empty, tenantId),
                cancellationToken).ConfigureAwait(false);
            var messageId = SafeToken(transport.ProviderMessageId, 255);
            var providerStatus = SafeToken(transport.Detail, 64);
            return await FinishCanaryAsync(
                tenantId,
                invoiceNumber,
                participantId,
                invoiceId,
                EinvoiceSubmissionStatuses.Sent,
                failureReason: null,
                messageId,
                providerStatus,
                attemptedAt,
                cancellationToken,
                submissionId).ConfigureAwait(false);
        }
        catch (PeppolTransportException ex)
        {
            return await FinishCanaryAsync(
                tenantId,
                invoiceNumber,
                participantId,
                invoiceId,
                EinvoiceSubmissionStatuses.Failed,
                SafeReason(ex.Code),
                providerMessageId: null,
                providerStatus: SafeToken(ex.StatusCode?.ToString(), 64),
                attemptedAt,
                cancellationToken,
                submissionId).ConfigureAwait(false);
        }
    }

    private async Task<PeppolSubmission> FinishCanaryAsync(
        Guid tenantId,
        string invoiceNumber,
        string? participantId,
        Guid? invoiceId,
        string status,
        string? failureReason,
        string? providerMessageId,
        string? providerStatus,
        DateTime? attemptedAtUtc,
        CancellationToken cancellationToken,
        Guid? submissionId = null)
    {
        var id = submissionId ?? Guid.NewGuid();
        if (_db is not null && invoiceId is Guid invoice && invoice != Guid.Empty)
        {
            _db.EinvoiceSubmissions.Add(new EinvoiceSubmission
            {
                Id = id,
                TenantId = tenantId,
                InvoiceId = invoice,
                Status = status,
                CorrelationId = Guid.NewGuid(),
                FailureReason = failureReason,
                ProviderMessageId = providerMessageId,
                ProviderStatus = providerStatus,
                AttemptedAtUtc = attemptedAtUtc,
                AckedAtUtc = null,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogInformation(
            "Peppol canary submission finished. Status={Status} Reason={Reason}",
            status,
            failureReason);

        var memoryStatus = status switch
        {
            EinvoiceSubmissionStatuses.Sent => PeppolSubmissionStatus.Sent,
            EinvoiceSubmissionStatuses.Failed => PeppolSubmissionStatus.Failed,
            _ => PeppolSubmissionStatus.Queued,
        };
        var row = new PeppolSubmission(
            id.ToString("N"),
            tenantId,
            invoiceNumber,
            memoryStatus,
            participantId,
            failureReason);
        _store.Save(row);
        if (memoryStatus == PeppolSubmissionStatus.Failed)
        {
            await AuditAsync(
                "EINVOICE_SUBMISSION_FAILED",
                AuditEventType.EinvoiceSubmissionFailed,
                tenantId,
                row.Id,
                failureReason,
                cancellationToken).ConfigureAwait(false);
        }
        else if (memoryStatus == PeppolSubmissionStatus.Sent)
        {
            await AuditAsync(
                "EINVOICE_SUBMITTED",
                AuditEventType.EinvoiceSubmitted,
                tenantId,
                row.Id,
                "sent",
                cancellationToken).ConfigureAwait(false);
        }

        return row;
    }

    private IPeppolAccessPointClient? ResolveCanaryClient(string provider)
    {
        if (_storecove is not null)
        {
            var selected = PeppolAccessPointClientFactory.Select(provider, _hosted, _storecove);
            return selected is StorecovePeppolAccessPointClient ? selected : null;
        }

        if (_canaryTransport is not null
            && string.Equals(provider, "storecove", StringComparison.OrdinalIgnoreCase))
        {
            return _canaryTransport;
        }

        return null;
    }

    private string? SafeToken(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Contains('<', StringComparison.Ordinal) || ContainsApiKey(trimmed))
            return null;

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private string SafeReason(string? code)
    {
        var token = SafeToken(code, 512);
        return token ?? "storecove-transport";
    }

    private bool ContainsApiKey(string value)
    {
        var storecoveKey = _options.Storecove.ApiKey;
        if (!string.IsNullOrWhiteSpace(storecoveKey)
            && value.Contains(storecoveKey, StringComparison.Ordinal))
        {
            return true;
        }

        var hostedKey = _options.ApiKey;
        return !string.IsNullOrWhiteSpace(hostedKey)
            && value.Contains(hostedKey, StringComparison.Ordinal);
    }

    private async Task QueueReservedAsync(Guid tenantId, Guid? invoiceId, CancellationToken cancellationToken)
    {
        if (_db is null || invoiceId is not Guid invoice || invoice == Guid.Empty)
            return;

        var now = DateTime.UtcNow;
        _db.EinvoiceSubmissions.Add(new EinvoiceSubmission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice,
            Status = EinvoiceSubmissionStatuses.Queued,
            CorrelationId = Guid.NewGuid(),
            FailureReason = ReservedFailureReason,
            CreatedAtUtc = now,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task AuditAsync(
        string action,
        AuditEventType type,
        Guid tenantId,
        string entityId,
        string? detail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_audit is not null)
        {
            await _audit.LogSystemOperationAsync(
                action: action,
                entityType: "PeppolSubmission",
                userId: "system",
                userRole: "SuperAdmin",
                description: detail,
                actionType: type,
                tenantId: tenantId).ConfigureAwait(false);
        }

        if (_activity is not null && ToActivity(type) is ActivityEventType activityType)
        {
            await _activity.TryPublishAsync(
                tenantId,
                activityType,
                metadata: new { detail },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static ActivityEventType? ToActivity(AuditEventType type) => type switch
    {
        AuditEventType.EinvoiceValidated => ActivityEventType.EinvoiceValidated,
        AuditEventType.EinvoiceSubmitted => ActivityEventType.EinvoiceSubmitted,
        AuditEventType.EinvoiceSubmissionFailed => ActivityEventType.EinvoiceSubmissionFailed,
        _ => null,
    };
}
