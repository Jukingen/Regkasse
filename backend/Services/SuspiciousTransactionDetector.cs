using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>Tenant-scoped payment anomaly rules (high value, storno/refund bursts, unusual hours).</summary>
public sealed class SuspiciousTransactionDetector
{
    private const string CardPaymentMethodRaw = "1";

    private readonly AppDbContext _db;
    private readonly ISuspiciousTransactionAlertService _alerts;
    private readonly IOptionsMonitor<SuspiciousTransactionDetectionOptions> _options;

    public SuspiciousTransactionDetector(
        AppDbContext db,
        ISuspiciousTransactionAlertService alerts,
        IOptionsMonitor<SuspiciousTransactionDetectionOptions> options)
    {
        _db = db;
        _alerts = alerts;
        _options = options;
    }

    public async Task DetectForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Enabled)
            return;

        var registerIds = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(cr => cr.TenantId == tenantId)
            .Select(cr => cr.Id)
            .ToListAsync(cancellationToken);

        if (registerIds.Count == 0)
            return;

        await DetectHighValueAsync(tenantId, registerIds, cancellationToken);
        await DetectMultipleStornosAsync(tenantId, registerIds, cancellationToken);
        await DetectMultipleRefundsAsync(tenantId, registerIds, cancellationToken);
        await DetectUnusualTimeAsync(tenantId, registerIds, cancellationToken);
        await DetectSameCardMultipleAsync(tenantId, registerIds, cancellationToken);
        await DetectRapidTransactionsAsync(tenantId, registerIds, cancellationToken);
    }

    private IQueryable<PaymentDetails> TenantPayments(IReadOnlyCollection<Guid> registerIds) =>
        _db.PaymentDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => registerIds.Contains(p.CashRegisterId));

    private async Task DetectHighValueAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var since = DateTime.UtcNow.AddMinutes(-Math.Max(1, opts.HighValueLookbackMinutes));
        var transactions = await TenantPayments(registerIds)
            .Where(p => !p.IsStorno && !p.IsRefund && p.IsActive && p.TotalAmount >= opts.HighValueThresholdEur)
            .Where(p => p.CreatedAt > since)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            await _alerts.TryPublishAlertAsync(
                new SuspiciousAlertDraft(
                    tenantId,
                    SuspiciousAlertType.HighValue,
                    SuspiciousAlertSeverity.High,
                    $"Hohe Zahlung von €{transaction.TotalAmount:F2}",
                    "Prüfen Sie die Transaktion auf Betrug",
                    $"high_value_{transaction.Id:N}",
                    PaymentId: transaction.Id,
                    Details: new
                    {
                        transaction.TotalAmount,
                        Method = transaction.PaymentMethod.ToString(),
                        transaction.ReceiptNumber,
                    }),
                cancellationToken);
        }
    }

    private async Task DetectMultipleStornosAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var since = DateTime.UtcNow.AddHours(-1);
        var cashiers = await TenantPayments(registerIds)
            .Where(p => p.IsStorno && p.CreatedAt > since && p.CashierId != null && p.CashierId != "")
            .GroupBy(p => p.CashierId!)
            .Select(g => new { CashierId = g.Key, Count = g.Count() })
            .Where(g => g.Count >= opts.MaxStornosPerHour)
            .ToListAsync(cancellationToken);

        foreach (var cashier in cashiers)
        {
            await _alerts.TryPublishAlertAsync(
                new SuspiciousAlertDraft(
                    tenantId,
                    SuspiciousAlertType.MultipleStornos,
                    SuspiciousAlertSeverity.Medium,
                    $"Kassierer {cashier.CashierId} hat {cashier.Count} Stornos in der letzten Stunde",
                    "Überprüfen Sie die Storno-Aktivitäten dieses Kassierers",
                    $"stornos_{cashier.CashierId}_{since:yyyyMMddHH}",
                    UserId: cashier.CashierId,
                    Details: new { cashier.CashierId, cashier.Count }),
                cancellationToken);
        }
    }

    private async Task DetectMultipleRefundsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var since = DateTime.UtcNow.AddDays(-1);
        var customers = await TenantPayments(registerIds)
            .Where(p => p.IsRefund && p.CreatedAt > since)
            .GroupBy(p => p.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(p => Math.Abs(p.TotalAmount)),
            })
            .Where(g => g.Count >= opts.MaxRefundsPerDay)
            .ToListAsync(cancellationToken);

        foreach (var customer in customers)
        {
            await _alerts.TryPublishAlertAsync(
                new SuspiciousAlertDraft(
                    tenantId,
                    SuspiciousAlertType.MultipleRefunds,
                    SuspiciousAlertSeverity.High,
                    $"Kunde hat {customer.Count} Rückerstattungen (€{customer.TotalAmount:F2}) in 24h",
                    "Kundenkonto auf betrügerische Aktivitäten prüfen",
                    $"refunds_{customer.CustomerId:N}_{since:yyyyMMdd}",
                    CustomerId: customer.CustomerId,
                    Details: new { customer.CustomerId, customer.Count, customer.TotalAmount }),
                cancellationToken);
        }
    }

    private async Task DetectUnusualTimeAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var since = DateTime.UtcNow.AddMinutes(-Math.Max(1, opts.UnusualTimeLookbackMinutes));
        var sales = await TenantPayments(registerIds)
            .Where(p => !p.IsStorno && !p.IsRefund && p.IsActive && p.CreatedAt > since)
            .Select(p => new { p.Id, p.CreatedAt, p.TotalAmount, p.ReceiptNumber })
            .ToListAsync(cancellationToken);

        foreach (var sale in sales)
        {
            if (!IsUnusualLocalHour(sale.CreatedAt))
                continue;

            await _alerts.TryPublishAlertAsync(
                new SuspiciousAlertDraft(
                    tenantId,
                    SuspiciousAlertType.UnusualTime,
                    SuspiciousAlertSeverity.Medium,
                    $"Zahlung außerhalb der üblichen Geschäftszeiten (Beleg {sale.ReceiptNumber ?? sale.Id.ToString()})",
                    "Transaktion auf Unregelmäßigkeit prüfen",
                    $"unusual_time_{sale.Id:N}",
                    PaymentId: sale.Id,
                    Details: new { sale.TotalAmount, sale.ReceiptNumber, sale.CreatedAt }),
                cancellationToken);
        }
    }

    private bool IsUnusualLocalHour(DateTime createdAtUtc)
    {
        var opts = _options.CurrentValue;
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc),
            PostgreSqlUtcDateTime.AustriaTimeZone);
        var hour = local.Hour;
        var start = opts.UnusualHourStart;
        var end = opts.UnusualHourEnd;
        if (start < end)
            return hour >= start && hour < end;
        return hour >= start || hour < end;
    }

    private async Task DetectSameCardMultipleAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var since = DateTime.UtcNow.AddHours(-1);
        var groups = await TenantPayments(registerIds)
            .Where(p => !p.IsStorno && !p.IsRefund && p.IsActive && p.CreatedAt > since)
            .Where(p => p.PaymentMethodRaw == CardPaymentMethodRaw)
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count(), Total = g.Sum(p => p.TotalAmount) })
            .Where(g => g.Count >= opts.SameCardPaymentsPerHour)
            .ToListAsync(cancellationToken);

        foreach (var row in groups)
        {
            await _alerts.TryPublishAlertAsync(
                new SuspiciousAlertDraft(
                    tenantId,
                    SuspiciousAlertType.SameCardMultiple,
                    SuspiciousAlertSeverity.High,
                    $"Kunde {row.CustomerId} hat {row.Count} Kartenzahlungen in der letzten Stunde",
                    "Mehrfach-Kartenzahlungen auf Betrug prüfen",
                    $"card_multi_{row.CustomerId:N}_{since:yyyyMMddHH}",
                    CustomerId: row.CustomerId,
                    Details: new { row.CustomerId, row.Count, row.Total }),
                cancellationToken);
        }
    }

    private async Task DetectRapidTransactionsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> registerIds,
        CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var since = DateTime.UtcNow.AddHours(-1);
        var cashiers = await TenantPayments(registerIds)
            .Where(p => !p.IsStorno && !p.IsRefund && p.IsActive && p.CreatedAt > since)
            .Where(p => p.CashierId != null && p.CashierId != "")
            .GroupBy(p => p.CashierId!)
            .Select(g => new { CashierId = g.Key, Count = g.Count(), Total = g.Sum(p => p.TotalAmount) })
            .Where(g => g.Count >= opts.RapidTransactionsPerHour)
            .ToListAsync(cancellationToken);

        foreach (var row in cashiers)
        {
            await _alerts.TryPublishAlertAsync(
                new SuspiciousAlertDraft(
                    tenantId,
                    SuspiciousAlertType.RapidTransactions,
                    SuspiciousAlertSeverity.Medium,
                    $"Kassierer {row.CashierId} hat {row.Count} Zahlungen in der letzten Stunde",
                    "Ungewöhnlich hohe Transaktionsfrequenz prüfen",
                    $"rapid_{row.CashierId}_{since:yyyyMMddHH}",
                    UserId: row.CashierId,
                    Details: new { row.CashierId, row.Count, row.Total }),
                cancellationToken);
        }
    }
}
