using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IAdminPaymentListService
{
    Task<(PaymentListResponse Response, string? ErrorCode, string? ErrorMessage)> QueryAsync(
        PaymentFilterDto filter,
        string? stornoReason,
        CancellationToken cancellationToken = default);
}

public sealed class AdminPaymentListService : IAdminPaymentListService
{
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly IPaymentMethodCatalogService _paymentMethodCatalog;

    public AdminPaymentListService(
        AppDbContext context,
        ISettingsTenantResolver settingsTenantResolver,
        IPaymentMethodCatalogService paymentMethodCatalog)
    {
        _context = context;
        _settingsTenantResolver = settingsTenantResolver;
        _paymentMethodCatalog = paymentMethodCatalog;
    }

    public async Task<(PaymentListResponse Response, string? ErrorCode, string? ErrorMessage)> QueryAsync(
        PaymentFilterDto filter,
        string? stornoReason,
        CancellationToken cancellationToken = default)
    {
        filter = PaymentQueryExtensions.Normalize(filter);
        var nowUtc = DateTime.UtcNow;
        var usedDefaultDateWindow = !filter.StartDate.HasValue && !filter.EndDate.HasValue;

        if (filter.StartDate.HasValue && filter.EndDate.HasValue && filter.StartDate > filter.EndDate)
            return (new PaymentListResponse(), "ADMIN_PAYMENTS_INVALID_RANGE", "startDate must be <= endDate");

        if (filter.MinAmount.HasValue && filter.MaxAmount.HasValue && filter.MinAmount > filter.MaxAmount)
            return (new PaymentListResponse(), "ADMIN_PAYMENTS_INVALID_AMOUNT_RANGE", "minAmount must be <= maxAmount");

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        if (filter.CashRegisterId.HasValue && filter.CashRegisterId.Value != Guid.Empty)
        {
            var regOk = await _context.CashRegisters.AsNoTracking().ForResolvedTenantScope()
                .AnyAsync(cr => cr.Id == filter.CashRegisterId.Value && cr.TenantId == tenantId, cancellationToken);
            if (!regOk)
                return (new PaymentListResponse(), "ADMIN_PAYMENTS_INVALID_REGISTER", "Cash register is not in the current tenant");
        }

        var methodRaws = await ResolveMethodRawsAsync(filter.PaymentMethods, cancellationToken);
        var availableMethods = await LoadAvailablePaymentMethodsAsync(tenantId, cancellationToken);

        IQueryable<PaymentDetails> query = _context.PaymentDetails.AsNoTracking()
            .ApplyTenantCashRegisterScope(
                _context.CashRegisters.AsNoTracking().ForResolvedTenantScope(),
                tenantId);

        query = query.ApplyDateRangeFilter(filter, nowUtc);
        query = query.ApplyAmountRangeFilter(filter);
        query = query.ApplyPaymentMethodFilter(methodRaws);
        query = query.ApplyCashRegisterFilter(filter.CashRegisterId);
        query = query.ApplyStatusFilter(filter.Statuses);
        query = query.ApplyReversalTypeFilter(filter.IsStorno, filter.IsRefund);

        if (!string.IsNullOrWhiteSpace(stornoReason) &&
            Enum.TryParse<StornoReason>(stornoReason.Trim(), ignoreCase: true, out var reasonEnum))
        {
            query = query.Where(p => p.IsStorno && p.StornoReason == reasonEnum);
        }

        query = query.ApplyCustomerFilters(filter);
        query = query.ApplyCashierFilter(filter.CashierId);
        query = query.ApplyReceiptNumberFilter(filter.ReceiptNumber);

        int? total = null;
        if (filter.IncludeTotalCount)
            total = await query.CountAsync(cancellationToken);

        var sorted = query.ApplySorting(filter);
        var hasDecodedCursor = KeysetCursor.TryDecode(filter.AfterCursor, out var afterCursor);
        var useKeyset = PaymentQueryExtensions.SupportsKeysetPagination(filter) && hasDecodedCursor;

        IQueryable<PaymentDetails> pageQuery = sorted;
        if (useKeyset)
        {
            pageQuery = sorted.ApplyKeysetAfterDesc(afterCursor, p => p.CreatedAt, p => p.Id);
        }
        else if (filter.Page > 1)
        {
            pageQuery = sorted.Skip((filter.Page - 1) * filter.PageSize);
        }

        var coreRows = await pageQuery
            .Take(filter.PageSize + 1)
            .Select(p => new PaymentListCoreRow
            {
                Id = p.Id,
                TransactionId = p.TransactionId,
                CreatedAt = p.CreatedAt,
                TotalAmount = p.TotalAmount,
                PaymentMethodRaw = p.PaymentMethodRaw,
                CustomerName = p.CustomerName,
                CashRegisterId = p.CashRegisterId,
                ReceiptNumber = p.ReceiptNumber,
                OfflineTransactionId = p.OfflineTransactionId,
                OfflineReplayBatchCorrelationId = p.OfflineReplayBatchCorrelationId,
                FinanzOnlineStatus = p.FinanzOnlineStatus,
                FinanzOnlineError = p.FinanzOnlineError,
                FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                FinanzOnlineRetryCount = p.FinanzOnlineRetryCount,
                IsStorno = p.IsStorno,
                IsRefund = p.IsRefund,
                IsActive = p.IsActive,
                StornoReason = p.StornoReason,
                OriginalPaymentId = p.OriginalPaymentId,
                CashierId = p.CashierId,
            })
            .ToListAsync(cancellationToken);

        var hasMore = coreRows.Count > filter.PageSize;
        if (hasMore)
            coreRows = coreRows.Take(filter.PageSize).ToList();

        var items = await EnrichListItemsAsync(coreRows, cancellationToken);

        string? nextCursor = null;
        if (hasMore && coreRows.Count > 0)
        {
            var last = coreRows[^1];
            nextCursor = new KeysetCursor(last.CreatedAt, last.Id).Encode();
        }

        var activeFilters = PaymentQueryExtensions.BuildFilterSummary(filter, availableMethods, usedDefaultDateWindow);
        if (!string.IsNullOrWhiteSpace(stornoReason))
        {
            activeFilters.AppliedFilters["stornoReason"] = stornoReason.Trim();
            activeFilters.ActiveFilterCount++;
        }

        return (new PaymentListResponse
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize,
            NextCursor = nextCursor,
            HasMore = hasMore,
            ActiveFilters = activeFilters,
        }, null, null);
    }

    private async Task<List<PaymentListItemDto>> EnrichListItemsAsync(
        IReadOnlyList<PaymentListCoreRow> coreRows,
        CancellationToken cancellationToken)
    {
        if (coreRows.Count == 0)
            return [];

        var paymentIds = coreRows.Select(p => p.Id).ToList();
        var originalPaymentIds = coreRows
            .Where(p => p.OriginalPaymentId.HasValue)
            .Select(p => p.OriginalPaymentId!.Value)
            .Distinct()
            .ToList();
        var cashierIds = coreRows
            .Select(p => p.CashierId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var receiptsTask = _context.Receipts.AsNoTracking()
            .Where(r => paymentIds.Contains(r.PaymentId))
            .Select(r => new { r.PaymentId, r.ReceiptId })
            .ToListAsync(cancellationToken);

        var invoicesTask = _context.Invoices.AsNoTracking()
            .Where(i => i.SourcePaymentId != null && paymentIds.Contains(i.SourcePaymentId.Value))
            .Select(i => new { PaymentId = i.SourcePaymentId!.Value, i.Id, i.InvoiceNumber })
            .ToListAsync(cancellationToken);

        var voucherAggTask = _context.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.PaymentId != null
                && paymentIds.Contains(l.PaymentId.Value)
                && l.Type == VoucherTransactionType.Redeem)
            .GroupBy(l => l.PaymentId!.Value)
            .Select(g => new { PaymentId = g.Key, Amount = g.Sum(l => -l.Amount) })
            .ToListAsync(cancellationToken);

        Task<List<OriginalReceiptRow>> originalReceiptsTask = originalPaymentIds.Count == 0
            ? Task.FromResult(new List<OriginalReceiptRow>())
            : _context.PaymentDetails.AsNoTracking()
                .Where(p => originalPaymentIds.Contains(p.Id))
                .Select(p => new OriginalReceiptRow { Id = p.Id, ReceiptNumber = p.ReceiptNumber })
                .ToListAsync(cancellationToken);

        Task<List<CashierNameRow>> cashiersTask = cashierIds.Count == 0
            ? Task.FromResult(new List<CashierNameRow>())
            : _context.Users.AsNoTracking()
                .Where(u => cashierIds.Contains(u.Id))
                .Select(u => new CashierNameRow
                {
                    Id = u.Id,
                    DisplayName = u.FirstName + " " + u.LastName,
                })
                .ToListAsync(cancellationToken);

        await Task.WhenAll(receiptsTask, invoicesTask, voucherAggTask, originalReceiptsTask, cashiersTask);

        var receiptByPayment = receiptsTask.Result
            .GroupBy(r => r.PaymentId)
            .ToDictionary(g => g.Key, g => (Guid?)g.First().ReceiptId);
        var invoiceByPayment = invoicesTask.Result
            .GroupBy(i => i.PaymentId)
            .ToDictionary(g => g.Key, g => g.First());
        var voucherByPayment = voucherAggTask.Result.ToDictionary(v => v.PaymentId, v => v.Amount);
        var originalReceiptByPaymentId = originalReceiptsTask.Result.ToDictionary(p => p.Id, p => p.ReceiptNumber);
        var cashierById = cashiersTask.Result.ToDictionary(c => c.Id, c => c.DisplayName, StringComparer.Ordinal);

        return coreRows.Select(p =>
        {
            invoiceByPayment.TryGetValue(p.Id, out var invoice);
            voucherByPayment.TryGetValue(p.Id, out var voucherAmount);
            var hasVoucher = voucherByPayment.ContainsKey(p.Id);
            string? cashierName = null;
            if (!string.IsNullOrWhiteSpace(p.CashierId))
                cashierById.TryGetValue(p.CashierId, out cashierName);

            string? originalReceiptNumber = null;
            if (p.OriginalPaymentId.HasValue)
                originalReceiptByPaymentId.TryGetValue(p.OriginalPaymentId.Value, out originalReceiptNumber);

            return new PaymentListItemDto
            {
                Id = p.Id,
                TransactionId = p.TransactionId,
                CreatedAt = p.CreatedAt,
                TotalAmount = p.TotalAmount,
                Currency = "EUR",
                Method = AdminPaymentListMapper.ParsePaymentMethodName(p.PaymentMethodRaw),
                Status = AdminPaymentListMapper.ResolvePaymentStatus(p.IsStorno, p.IsRefund, p.IsActive, p.FinanzOnlineStatus),
                CustomerName = p.CustomerName,
                CashRegisterId = p.CashRegisterId,
                ReceiptNumber = p.ReceiptNumber,
                ReceiptId = receiptByPayment.GetValueOrDefault(p.Id),
                InvoiceId = invoice?.Id,
                InvoiceNumber = invoice?.InvoiceNumber,
                IsOfflineOrigin = p.OfflineTransactionId != null,
                OfflineTransactionId = p.OfflineTransactionId,
                OfflineReplayBatchCorrelationId = p.OfflineReplayBatchCorrelationId,
                FinanzOnlineStatus = p.FinanzOnlineStatus,
                FinanzOnlineError = p.FinanzOnlineError,
                FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                FinanzOnlineRetryCount = p.FinanzOnlineRetryCount,
                InvoicePersisted = invoice != null,
                VoucherRedeemedAmount = voucherAmount,
                HasVoucherRedemption = hasVoucher,
                IsStorno = p.IsStorno,
                IsRefund = p.IsRefund,
                StornoReason = p.StornoReason,
                OriginalPaymentId = p.OriginalPaymentId,
                OriginalReceiptNumber = originalReceiptNumber,
                CashierDisplayName = cashierName,
                ReversalCompletionStatus =
                    p.FinanzOnlineStatus != null &&
                    p.FinanzOnlineStatus.ToLower() == "failed"
                        ? "Failed"
                        : "Completed",
            };
        }).ToList();
    }

    private async Task<List<string>> ResolveMethodRawsAsync(
        IReadOnlyList<string> paymentMethods,
        CancellationToken cancellationToken)
    {
        if (paymentMethods.Count == 0)
            return [];

        var resolveTasks = paymentMethods
            .Select(method => _paymentMethodCatalog.ResolveRawForFilterAsync(method, cancellationToken: cancellationToken))
            .ToList();
        var resolved = await Task.WhenAll(resolveTasks);

        var raws = new List<string>();
        foreach (var raw in resolved)
        {
            if (!raws.Contains(raw, StringComparer.Ordinal))
                raws.Add(raw);
        }

        return raws;
    }

    private async Task<IReadOnlyList<string>> LoadAvailablePaymentMethodsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var fromDefinitions = await _context.PaymentMethodDefinitions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .Select(x => x.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (fromDefinitions.Count > 0)
            return fromDefinitions;

        return Enum.GetNames(typeof(PaymentMethod)).ToList();
    }

    private sealed class PaymentListCoreRow
    {
        public Guid Id { get; init; }
        public string? TransactionId { get; init; }
        public DateTime CreatedAt { get; init; }
        public decimal TotalAmount { get; init; }
        public string PaymentMethodRaw { get; init; } = string.Empty;
        public string? CustomerName { get; init; }
        public Guid CashRegisterId { get; init; }
        public string? ReceiptNumber { get; init; }
        public Guid? OfflineTransactionId { get; init; }
        public Guid? OfflineReplayBatchCorrelationId { get; init; }
        public string? FinanzOnlineStatus { get; init; }
        public string? FinanzOnlineError { get; init; }
        public string? FinanzOnlineReferenceId { get; init; }
        public DateTime? FinanzOnlineLastAttemptAtUtc { get; init; }
        public int FinanzOnlineRetryCount { get; init; }
        public bool IsStorno { get; init; }
        public bool IsRefund { get; init; }
        public bool IsActive { get; init; }
        public StornoReason? StornoReason { get; init; }
        public Guid? OriginalPaymentId { get; init; }
        public string? CashierId { get; init; }
    }

    private sealed class OriginalReceiptRow
    {
        public Guid Id { get; init; }
        public string? ReceiptNumber { get; init; }
    }

    private sealed class CashierNameRow
    {
        public string Id { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
    }
}

public static class AdminPaymentListMapper
{
    public static string ResolvePaymentStatus(PaymentDetails p) =>
        ResolvePaymentStatus(p.IsStorno, p.IsRefund, p.IsActive, p.FinanzOnlineStatus);

    public static string ResolvePaymentStatus(bool isStorno, bool isRefund, bool isActive, string? finanzOnlineStatus)
    {
        if (isStorno || !isActive)
            return "Cancelled";
        if (isRefund)
            return "Refunded";
        if (!string.IsNullOrWhiteSpace(finanzOnlineStatus) &&
            string.Equals(finanzOnlineStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            return "Failed";
        if (!string.IsNullOrWhiteSpace(finanzOnlineStatus) &&
            (string.Equals(finanzOnlineStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(finanzOnlineStatus, "NeedsReconciliation", StringComparison.OrdinalIgnoreCase)))
            return "Pending";
        return "Success";
    }

    public static string ParsePaymentMethodName(string rawValue)
    {
        if (int.TryParse(rawValue, out var methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            return ((PaymentMethod)methodInt).ToString();
        return "Unknown";
    }
}
