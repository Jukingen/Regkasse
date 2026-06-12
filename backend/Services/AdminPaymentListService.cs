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

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .ApplySorting(filter)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => new PaymentListItemDto
            {
                Id = p.Id,
                TransactionId = p.TransactionId,
                CreatedAt = p.CreatedAt,
                TotalAmount = p.TotalAmount,
                Currency = "EUR",
                Method = AdminPaymentListMapper.ParsePaymentMethodName(p.PaymentMethodRaw),
                Status = AdminPaymentListMapper.ResolvePaymentStatus(p),
                CustomerName = p.CustomerName,
                CashRegisterId = p.CashRegisterId,
                ReceiptNumber = p.ReceiptNumber,
                ReceiptId = _context.Receipts
                    .Where(r => r.PaymentId == p.Id)
                    .Select(r => (Guid?)r.ReceiptId)
                    .FirstOrDefault(),
                InvoiceId = _context.Invoices
                    .Where(i => i.SourcePaymentId == p.Id)
                    .Select(i => (Guid?)i.Id)
                    .FirstOrDefault(),
                InvoiceNumber = _context.Invoices
                    .Where(i => i.SourcePaymentId == p.Id)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefault(),
                IsOfflineOrigin = p.OfflineTransactionId != null,
                OfflineTransactionId = p.OfflineTransactionId,
                OfflineReplayBatchCorrelationId = p.OfflineReplayBatchCorrelationId,
                FinanzOnlineStatus = p.FinanzOnlineStatus,
                FinanzOnlineError = p.FinanzOnlineError,
                FinanzOnlineReferenceId = p.FinanzOnlineReferenceId,
                FinanzOnlineLastAttemptAtUtc = p.FinanzOnlineLastAttemptAtUtc,
                FinanzOnlineRetryCount = p.FinanzOnlineRetryCount,
                InvoicePersisted = _context.Invoices.Any(i => i.SourcePaymentId == p.Id),
                VoucherRedeemedAmount = _context.VoucherLedgerEntries
                    .Where(l => l.PaymentId == p.Id && l.Type == VoucherTransactionType.Redeem)
                    .Select(l => (decimal?)(-l.Amount))
                    .Sum() ?? 0m,
                HasVoucherRedemption = _context.VoucherLedgerEntries
                    .Any(l => l.PaymentId == p.Id && l.Type == VoucherTransactionType.Redeem),
                IsStorno = p.IsStorno,
                IsRefund = p.IsRefund,
                StornoReason = p.StornoReason,
                OriginalPaymentId = p.OriginalPaymentId,
                OriginalReceiptNumber = _context.PaymentDetails
                    .Where(op => op.Id == p.OriginalPaymentId)
                    .Select(op => op.ReceiptNumber)
                    .FirstOrDefault(),
                CashierDisplayName = _context.Users
                    .Where(u => u.Id == p.CashierId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault(),
                ReversalCompletionStatus =
                    p.FinanzOnlineStatus != null &&
                    p.FinanzOnlineStatus.ToLower() == "failed"
                        ? "Failed"
                        : "Completed"
            })
            .ToListAsync(cancellationToken);

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
            ActiveFilters = activeFilters,
        }, null, null);
    }

    private async Task<List<string>> ResolveMethodRawsAsync(
        IReadOnlyList<string> paymentMethods,
        CancellationToken cancellationToken)
    {
        var raws = new List<string>();
        foreach (var method in paymentMethods)
        {
            var raw = await _paymentMethodCatalog.ResolveRawForFilterAsync(method, cancellationToken: cancellationToken);
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
}

public static class AdminPaymentListMapper
{
    public static string ResolvePaymentStatus(PaymentDetails p)
    {
        if (p.IsStorno || !p.IsActive)
            return "Cancelled";
        if (p.IsRefund)
            return "Refunded";
        if (!string.IsNullOrWhiteSpace(p.FinanzOnlineStatus) &&
            string.Equals(p.FinanzOnlineStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            return "Failed";
        if (!string.IsNullOrWhiteSpace(p.FinanzOnlineStatus) &&
            (string.Equals(p.FinanzOnlineStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(p.FinanzOnlineStatus, "NeedsReconciliation", StringComparison.OrdinalIgnoreCase)))
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
