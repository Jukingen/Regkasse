using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Middleware;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// Audit and activity feed for a built QR-Rechnung. Never writes the SPC text or the IBAN.
/// </summary>
internal static class QrRechnungAudit
{
    public static string HashPayload(string swissQrText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(swissQrText ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static void EnsureRelativePdfPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Path.IsPathRooted(path)
            || path.Contains(':', StringComparison.Ordinal)
            || path.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "QR-Rechnung PDF path must be relative.",
                nameof(path));
        }
    }

    public static string CorrelationId(IHttpContextAccessor? httpContextAccessor)
    {
        var fromRequest = httpContextAccessor?.HttpContext?.Items[CorrelationIdMiddleware.CorrelationIdItemKey] as string;
        return string.IsNullOrWhiteSpace(fromRequest)
            ? Guid.NewGuid().ToString("D")
            : fromRequest;
    }

    public static async Task PayloadBuiltAsync(
        IAuditLogService? audit,
        IActivityEventPublisher? activity,
        IHttpContextAccessor? httpContextAccessor,
        QrRechnungRequest request,
        QrRechnungPayload payload,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (audit is null && activity is null)
            return;

        var facts = Facts(request, payload, pdfPathRelative: null);
        var correlationId = CorrelationId(httpContextAccessor);
        await WriteAsync(
            audit,
            activity,
            AuditEventType.QrRechnungPayloadBuilt,
            ActivityEventType.QrRechnungPayloadBuilt,
            "QR_RECHNUNG_PAYLOAD_BUILT",
            "QR-Rechnung payload built",
            request,
            facts,
            correlationId,
            actorUserId,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task PdfGeneratedAsync(
        IAuditLogService? audit,
        IActivityEventPublisher? activity,
        IHttpContextAccessor? httpContextAccessor,
        QrRechnungRequest request,
        QrRechnungPayload payload,
        CancellationToken cancellationToken)
    {
        if (audit is null && activity is null)
            return;

        EnsureRelativePdfPath(request.PdfPathRelative);
        var facts = Facts(request, payload, request.PdfPathRelative);
        var correlationId = CorrelationId(httpContextAccessor);
        await WriteAsync(
            audit,
            activity,
            AuditEventType.QrRechnungPdfGenerated,
            ActivityEventType.QrRechnungPdfGenerated,
            "QR_RECHNUNG_PDF_GENERATED",
            "QR-Rechnung PDF generated",
            request,
            facts,
            correlationId,
            actorUserId: null,
            cancellationToken).ConfigureAwait(false);
    }

    private static object Facts(QrRechnungRequest request, QrRechnungPayload payload, string? pdfPathRelative) =>
        new
        {
            invoiceId = request.InvoiceId,
            tenantId = request.TenantId,
            payloadHash = HashPayload(payload.SwissQrText),
            referenceType = payload.ReferenceType.ToString(),
            pdfPathRelative,
        };

    private static async Task WriteAsync(
        IAuditLogService? audit,
        IActivityEventPublisher? activity,
        AuditEventType auditType,
        ActivityEventType activityType,
        string action,
        string description,
        QrRechnungRequest request,
        object facts,
        string correlationId,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (audit is not null)
        {
            try
            {
                await audit.LogSystemOperationAsync(
                    action: action,
                    entityType: "Invoice",
                    userId: string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId,
                    userRole: string.IsNullOrWhiteSpace(actorUserId) ? "system" : "Cashier",
                    description: description,
                    correlationIdOverride: correlationId,
                    actionType: auditType,
                    entityId: request.InvoiceId,
                    tenantId: request.TenantId,
                    newValues: facts).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A failed audit write must not drop the built bill. The payload is not logged.
            }
        }

        if (activity is not null && request.TenantId is Guid tenantId && tenantId != Guid.Empty)
        {
            try
            {
                await activity.TryPublishAsync(
                    tenantId,
                    activityType,
                    facts,
                    actorUserId,
                    dedupKey: request.InvoiceId is Guid invoiceId && invoiceId != Guid.Empty
                        ? $"{action}:{invoiceId:N}"
                        : null,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Activity feed is not the bill. Do not log the payload.
            }
        }
    }
}
