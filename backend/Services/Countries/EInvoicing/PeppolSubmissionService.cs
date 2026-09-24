using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

public interface IPeppolSubmissionService
{
    Task<PeppolSubmission> SubmitAsync(
        Guid tenantId,
        InvoiceDocumentDto document,
        string? participantId = null,
        CancellationToken cancellationToken = default);

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

    public PeppolSubmissionService(
        IEn16931XmlBuilder xml,
        IOptions<PeppolOptions> options,
        MockPeppolAccessPointClient mock,
        HostedPeppolAccessPointClient hosted,
        IPeppolSubmissionStore store,
        IAuditLogService? audit = null)
    {
        _xml = xml;
        _options = options.Value;
        _mock = mock;
        _hosted = hosted;
        _store = store;
        _audit = audit;
    }

    public async Task<PeppolSubmission> SubmitAsync(
        Guid tenantId,
        InvoiceDocumentDto document,
        string? participantId = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var invoiceNumber = document.InvoiceNumber ?? string.Empty;
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

    private async Task AuditAsync(
        string action,
        AuditEventType type,
        Guid tenantId,
        string entityId,
        string? detail,
        CancellationToken cancellationToken)
    {
        if (_audit is null)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        await _audit.LogSystemOperationAsync(
            action: action,
            entityType: "PeppolSubmission",
            userId: "system",
            userRole: "SuperAdmin",
            description: detail,
            actionType: type,
            tenantId: tenantId).ConfigureAwait(false);
    }
}
