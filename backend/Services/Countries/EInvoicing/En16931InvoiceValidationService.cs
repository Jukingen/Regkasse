using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

public interface IEn16931InvoiceValidationService
{
    /// <summary>
    /// Runs the embedded Schematron when <c>EInvoicing.En16931</c> is on.
    /// Returns null when the flag is off. Does not call a Peppol Access Point.
    /// </summary>
    Task<En16931ValidationResult?> ValidateAndStoreAsync(
        Guid invoiceId,
        InvoiceDocumentDto document,
        CancellationToken cancellationToken = default);
}

public sealed class En16931InvoiceValidationService : IEn16931InvoiceValidationService
{
    private readonly AppDbContext _db;
    private readonly IEn16931XmlBuilder _xml;
    private readonly IFeatureFlagService _featureFlags;
    private readonly IAuditLogService _audit;
    private readonly IActivityEventPublisher? _activity;

    public En16931InvoiceValidationService(
        AppDbContext db,
        IEn16931XmlBuilder xml,
        IFeatureFlagService featureFlags,
        IAuditLogService audit,
        IActivityEventPublisher? activity = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _xml = xml ?? throw new ArgumentNullException(nameof(xml));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _activity = activity;
    }

    public async Task<En16931ValidationResult?> ValidateAndStoreAsync(
        Guid invoiceId,
        InvoiceDocumentDto document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(row => row.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);
        if (invoice is null)
            throw new KeyNotFoundException($"Invoice '{invoiceId}' was not found.");

        if (!_featureFlags.IsEnabled(FeatureFlagNames.EInvoicingEn16931, invoice.TenantId.ToString("D")))
            return null;

        var xml = await _xml.BuildXmlAsync(document, cancellationToken).ConfigureAwait(false);
        var result = En16931Schematron.Evaluate(xml);
        invoice.EinvoiceValidationPassed = result.Passed;
        invoice.EinvoiceValidationRuleIds = result.Passed ? null : string.Join(',', result.RuleIds);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var detail = result.Passed ? "schematron-pass" : string.Join(',', result.RuleIds);
        await _audit.LogSystemOperationAsync(
            action: "EINVOICE_VALIDATED",
            entityType: "Invoice",
            userId: "system",
            userRole: Roles.SuperAdmin,
            description: detail,
            status: result.Passed ? AuditLogStatus.Success : AuditLogStatus.Failed,
            actionType: AuditEventType.EinvoiceValidated,
            entityId: invoice.Id,
            tenantId: invoice.TenantId).ConfigureAwait(false);

        if (result.Passed && _activity is not null)
        {
            await _activity.TryPublishAsync(
                invoice.TenantId,
                ActivityEventType.EinvoiceValidated,
                metadata: new { invoiceId = invoice.Id, detail },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
