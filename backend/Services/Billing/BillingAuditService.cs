using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Billing;

public interface IBillingAuditService
{
    Task LogLicenseSoldAsync(
        LicenseSale sale,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task LogLicenseCancelledAsync(
        LicenseSale sale,
        string actorUserId,
        string cancellationReason,
        CancellationToken cancellationToken = default);
}

/// <summary>Append-only billing audit (separate from RKSV/fiscal <see cref="AuditLog"/>).</summary>
public sealed class BillingAuditService : IBillingAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BillingAuditService> _logger;

    public BillingAuditService(AppDbContext db, ILogger<BillingAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogLicenseSoldAsync(
        LicenseSale sale,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        await AppendAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.LicenseSold,
            cancellationReason: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task LogLicenseCancelledAsync(
        LicenseSale sale,
        string actorUserId,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        await AppendAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.LicenseCancelled,
            cancellationReason,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task AppendAsync(
        LicenseSale sale,
        string actorUserId,
        string eventType,
        string? cancellationReason,
        CancellationToken cancellationToken)
    {
        try
        {
            _db.BillingAuditLogs.Add(new BillingAuditLog
            {
                LicenseSaleId = sale.Id,
                TenantId = sale.TenantId,
                EventType = eventType,
                ActorUserId = actorUserId,
                PriceNet = sale.PriceNet,
                PriceGross = sale.PriceGross,
                Currency = sale.Currency,
                InvoiceNumber = sale.InvoiceNumber,
                LicenseKey = sale.LicenseKey,
                LicensePlan = sale.LicensePlan,
                CancellationReason = cancellationReason,
                CreatedAtUtc = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Billing audit log write failed EventType={EventType} LicenseSaleId={LicenseSaleId}",
                eventType,
                sale.Id);
        }
    }
}
