using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Billing;

public interface IBillingAuditService
{
    Task LogLicenseSoldAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task LogLicenseCancelledAsync(
        LicenseSale sale,
        Guid actorUserId,
        string cancellationReason,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Append-only billing audit (separate from RKSV/fiscal <see cref="AuditLog"/>).</summary>
public sealed class BillingAuditService : IBillingAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ILogger<BillingAuditService> _logger;

    public BillingAuditService(AppDbContext db, ILogger<BillingAuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task LogLicenseSoldAsync(
        LicenseSale sale,
        Guid actorUserId,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        AppendAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.SaleCreated,
            cancellationReason: null,
            ipAddress,
            cancellationToken);

    public Task LogLicenseCancelledAsync(
        LicenseSale sale,
        Guid actorUserId,
        string cancellationReason,
        string? ipAddress = null,
        CancellationToken cancellationToken = default) =>
        AppendAsync(
            sale,
            actorUserId,
            BillingAuditEventTypes.SaleCancelled,
            cancellationReason,
            ipAddress,
            cancellationToken);

    private async Task AppendAsync(
        LicenseSale sale,
        Guid actorUserId,
        string action,
        string? cancellationReason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = JsonSerializer.Serialize(new BillingAuditDetails(
                sale.PriceNet,
                sale.PriceGross,
                sale.Currency,
                sale.InvoiceNumber,
                sale.LicenseKey,
                sale.LicensePlan,
                cancellationReason), JsonOptions);

            _db.BillingAuditLogs.Add(new BillingAuditLog
            {
                TenantId = sale.TenantId,
                UserId = actorUserId,
                Action = action,
                SaleId = sale.Id,
                Details = details,
                IpAddress = ipAddress,
                TimestampUtc = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Billing audit log write failed Action={Action} SaleId={SaleId}",
                action,
                sale.Id);
        }
    }

    private sealed record BillingAuditDetails(
        decimal PriceNet,
        decimal PriceGross,
        string Currency,
        string InvoiceNumber,
        string LicenseKey,
        string LicensePlan,
        string? CancellationReason);
}
