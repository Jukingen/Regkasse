using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>Sprint 5: Internal consistency checks – sequence issues, orphan refunds, payment without invoice. Read-only and auditable.</summary>
public class IntegrityCheckService : IIntegrityCheckService
{
    private static readonly Regex BelegNrRegex = new(@"^AT-(.+)-(\d{8})-(\d+)$", RegexOptions.Compiled);

    private readonly AppDbContext _context;
    private readonly ILogger<IntegrityCheckService> _logger;

    public IntegrityCheckService(AppDbContext context, ILogger<IntegrityCheckService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IntegrityReportDto> GetReportAsync(DateTime? fromDate = null, DateTime? toDate = null, bool includeDetails = false)
    {
        _logger.LogInformation("Integrity report requested: From={From}, To={To}, IncludeDetails={IncludeDetails}", fromDate, toDate, includeDetails);

        var from = fromDate?.Date ?? DateTime.MinValue;
        var to = toDate?.Date ?? DateTime.UtcNow.Date.AddDays(1);

        var sequence = await GetSequenceIssuesAsync(from, to, includeDetails);
        var orphanRefunds = await GetOrphanRefundsAsync(includeDetails);
        var paymentWithoutInvoice = await GetPaymentWithoutInvoiceCountAsync(from, to, includeDetails);

        return new IntegrityReportDto
        {
            SequenceIssues = sequence,
            OrphanRefunds = orphanRefunds,
            PaymentWithoutInvoice = paymentWithoutInvoice,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<SequenceIssuesDto> GetSequenceIssuesAsync(DateTime from, DateTime to, bool includeDetails)
    {
        var duplicateBelegNrs = new List<string>();
        var nonMonotonicKeys = new List<string>();

        var paymentNumbers = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p => p.IsActive && p.CreatedAt >= from && p.CreatedAt < to && !string.IsNullOrEmpty(p.ReceiptNumber))
            .Select(p => p.ReceiptNumber)
            .ToListAsync();

        var receiptNumbers = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.IssuedAt >= from && r.IssuedAt < to && !string.IsNullOrEmpty(r.ReceiptNumber))
            .Select(r => r.ReceiptNumber)
            .ToListAsync();

        var allNumbers = paymentNumbers.Concat(receiptNumbers).Distinct().ToList();
        var grouped = allNumbers.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        duplicateBelegNrs.AddRange(grouped);

        var parsed = allNumbers
            .Select(n => (Number: n, Match: BelegNrRegex.Match(n)))
            .Where(x => x.Match.Success)
            .Select(x => (x.Number, KassenId: x.Match.Groups[1].Value, DateStr: x.Match.Groups[2].Value, Seq: int.Parse(x.Match.Groups[3].Value)))
            .ToList();

        foreach (var group in parsed.GroupBy(x => (x.KassenId, x.DateStr)))
        {
            var ordered = group.OrderBy(x => x.Seq).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Seq <= ordered[i - 1].Seq)
                {
                    nonMonotonicKeys.Add($"{group.Key.KassenId}|{group.Key.DateStr}");
                    break;
                }
            }
        }

        var result = new SequenceIssuesDto
        {
            DuplicateReceiptNumberCount = duplicateBelegNrs.Count,
            NonMonotonicSequenceCount = nonMonotonicKeys.Distinct().Count()
        };
        if (includeDetails)
        {
            result.DuplicateReceiptNumbers = duplicateBelegNrs.Distinct().ToList();
            result.NonMonotonicKeys = nonMonotonicKeys.Distinct().ToList();
        }
        return result;
    }

    private async Task<OrphanRefundsDto> GetOrphanRefundsAsync(bool includeDetails)
    {
        var refunds = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p => p.IsActive && p.IsRefund)
            .Select(p => new { p.Id, p.OriginalPaymentId, p.ReceiptNumber })
            .ToListAsync();

        var paymentIds = refunds.Select(r => r.Id).ToHashSet();
        var originalIds = refunds.Where(r => r.OriginalPaymentId.HasValue).Select(r => r.OriginalPaymentId!.Value).Distinct().ToList();
        var existingOriginalIds = await _context.PaymentDetails
            .AsNoTracking()
            .Where(p => originalIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();
        var existingOriginalSet = existingOriginalIds.ToHashSet();

        var refundIdsWithInvoice = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.SourcePaymentId != null && paymentIds.Contains(i.SourcePaymentId.Value))
            .Select(i => i.SourcePaymentId!.Value)
            .Distinct()
            .ToListAsync();
        var withInvoiceSet = refundIdsWithInvoice.ToHashSet();

        var missingOriginal = refunds.Where(r => r.OriginalPaymentId == null || !existingOriginalSet.Contains(r.OriginalPaymentId.Value)).ToList();
        var missingInvoice = refunds.Where(r => !withInvoiceSet.Contains(r.Id)).ToList();
        var orphanIds = missingOriginal.Select(r => r.Id).Union(missingInvoice.Select(r => r.Id)).Distinct().ToList();

        var result = new OrphanRefundsDto
        {
            OrphanRefundCount = orphanIds.Count,
            MissingOriginalPaymentCount = missingOriginal.Count,
            RefundWithoutInvoiceCount = missingInvoice.Count
        };
        if (includeDetails)
        {
            result.OrphanPaymentIds = orphanIds;
            result.RefundReceiptNumbersMissingInvoice = missingInvoice.Select(r => r.ReceiptNumber).ToList();
        }
        return result;
    }

    private async Task<PaymentWithoutInvoiceDto> GetPaymentWithoutInvoiceCountAsync(DateTime from, DateTime to, bool includeDetails)
    {
        var query = _context.PaymentDetails
            .AsNoTracking()
            .Where(p => p.IsActive && !p.IsRefund && p.CreatedAt >= from && p.CreatedAt < to)
            .Where(p => !_context.Invoices.Any(i => i.SourcePaymentId == p.Id));

        var count = await query.CountAsync();
        var result = new PaymentWithoutInvoiceDto { Count = count };
        if (includeDetails)
        {
            result.PaymentIds = await query.Select(p => p.Id).Take(500).ToListAsync();
        }
        return result;
    }
}

// Sprint 5: Integrity report DTOs (same namespace)
public class IntegrityReportDto
{
    public SequenceIssuesDto SequenceIssues { get; set; } = new();
    public OrphanRefundsDto OrphanRefunds { get; set; } = new();
    public PaymentWithoutInvoiceDto PaymentWithoutInvoice { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}

public class SequenceIssuesDto
{
    public int DuplicateReceiptNumberCount { get; set; }
    public int NonMonotonicSequenceCount { get; set; }
    public List<string>? DuplicateReceiptNumbers { get; set; }
    public List<string>? NonMonotonicKeys { get; set; }
}

public class OrphanRefundsDto
{
    public int OrphanRefundCount { get; set; }
    public int MissingOriginalPaymentCount { get; set; }
    public int RefundWithoutInvoiceCount { get; set; }
    public List<Guid>? OrphanPaymentIds { get; set; }
    public List<string>? RefundReceiptNumbersMissingInvoice { get; set; }
}

public class PaymentWithoutInvoiceDto
{
    public int Count { get; set; }
    public List<Guid>? PaymentIds { get; set; }
}
