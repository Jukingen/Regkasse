using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public static class PaymentQueryExtensions
{
    public static readonly IReadOnlyList<string> KnownStatuses =
        ["Success", "Pending", "Failed", "Refunded", "Cancelled"];

    public static PaymentFilterDto Normalize(PaymentFilterDto filter)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = Math.Clamp(filter.PageSize, 1, 500);
        filter.SortBy = string.IsNullOrWhiteSpace(filter.SortBy) ? "CreatedAt" : filter.SortBy.Trim();
        filter.SortDirection = string.IsNullOrWhiteSpace(filter.SortDirection) ? "desc" : filter.SortDirection.Trim();
        filter.PaymentMethods = filter.PaymentMethods
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        filter.Statuses = filter.Statuses
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return filter;
    }

    public static IQueryable<PaymentDetails> ApplyTenantCashRegisterScope(
        this IQueryable<PaymentDetails> query,
        IQueryable<CashRegister> cashRegisters,
        Guid tenantId) =>
        query.Where(p => cashRegisters.Any(cr => cr.Id == p.CashRegisterId && cr.TenantId == tenantId));

    public static IQueryable<PaymentDetails> ApplyDateRangeFilter(
        this IQueryable<PaymentDetails> query,
        PaymentFilterDto filter,
        DateTime nowUtc)
    {
        if (!filter.StartDate.HasValue && !filter.EndDate.HasValue)
        {
            var fromUtc = nowUtc.AddDays(-30);
            return query.Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= nowUtc);
        }

        var startCal = filter.StartDate ?? filter.EndDate!.Value;
        var endCal = filter.EndDate ?? filter.StartDate!.Value;
        var (fromUtcRange, toExclusiveUtc) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(startCal, endCal);
        return query.Where(p => p.CreatedAt >= fromUtcRange && p.CreatedAt < toExclusiveUtc);
    }

    public static IQueryable<PaymentDetails> ApplyAmountRangeFilter(
        this IQueryable<PaymentDetails> query,
        PaymentFilterDto filter)
    {
        if (filter.MinAmount.HasValue)
            query = query.Where(p => p.TotalAmount >= filter.MinAmount.Value);
        if (filter.MaxAmount.HasValue)
            query = query.Where(p => p.TotalAmount <= filter.MaxAmount.Value);
        return query;
    }

    public static IQueryable<PaymentDetails> ApplyPaymentMethodFilter(
        this IQueryable<PaymentDetails> query,
        IReadOnlyList<string> methodRaws)
    {
        if (methodRaws.Count == 0)
            return query;
        return query.Where(p => methodRaws.Contains(p.PaymentMethodRaw));
    }

    public static IQueryable<PaymentDetails> ApplyStatusFilter(
        this IQueryable<PaymentDetails> query,
        IReadOnlyList<string> statuses)
    {
        if (statuses.Count == 0)
            return query;

        var normalized = statuses
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0)
            return query;

        return query.Where(p =>
            (normalized.Contains("Cancelled") && (p.IsStorno || !p.IsActive))
            || (normalized.Contains("Refunded") && p.IsRefund && p.IsActive && !p.IsStorno)
            || (normalized.Contains("Failed")
                && p.IsActive && !p.IsStorno && !p.IsRefund
                && p.FinanzOnlineStatus != null
                && p.FinanzOnlineStatus.ToLower() == "failed")
            || (normalized.Contains("Pending")
                && p.IsActive && !p.IsStorno && !p.IsRefund
                && p.FinanzOnlineStatus != null
                && (p.FinanzOnlineStatus.ToLower() == "pending"
                    || p.FinanzOnlineStatus.ToLower() == "needsreconciliation"))
            || (normalized.Contains("Success")
                && p.IsActive && !p.IsStorno && !p.IsRefund
                && (p.FinanzOnlineStatus == null
                    || (p.FinanzOnlineStatus.ToLower() != "failed"
                        && p.FinanzOnlineStatus.ToLower() != "pending"
                        && p.FinanzOnlineStatus.ToLower() != "needsreconciliation"))));
    }

    public static IQueryable<PaymentDetails> ApplyCashRegisterFilter(
        this IQueryable<PaymentDetails> query,
        Guid? cashRegisterId)
    {
        if (!cashRegisterId.HasValue || cashRegisterId.Value == Guid.Empty)
            return query;
        return query.Where(p => p.CashRegisterId == cashRegisterId.Value);
    }

    public static IQueryable<PaymentDetails> ApplyCustomerFilters(
        this IQueryable<PaymentDetails> query,
        PaymentFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            var name = $"%{filter.CustomerName.Trim()}%";
            query = query.Where(p => p.CustomerName != null && EF.Functions.ILike(p.CustomerName, name));
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerEmail))
        {
            var email = $"%{filter.CustomerEmail.Trim()}%";
            query = query.Where(p =>
                p.Customer != null
                && p.Customer.Email != null
                && EF.Functions.ILike(p.Customer.Email, email));
        }

        return query;
    }

    public static IQueryable<PaymentDetails> ApplyCashierFilter(
        this IQueryable<PaymentDetails> query,
        string? cashierId)
    {
        if (string.IsNullOrWhiteSpace(cashierId))
            return query;
        var normalized = cashierId.Trim();
        return query.Where(p => p.CashierId == normalized);
    }

    public static IQueryable<PaymentDetails> ApplyReceiptNumberFilter(
        this IQueryable<PaymentDetails> query,
        string? receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
            return query;
        var needle = $"%{receiptNumber.Trim()}%";
        return query.Where(p => p.ReceiptNumber != null && EF.Functions.ILike(p.ReceiptNumber, needle));
    }

    public static IQueryable<PaymentDetails> ApplyReversalTypeFilter(
        this IQueryable<PaymentDetails> query,
        bool? isStorno,
        bool? isRefund)
    {
        if (isStorno == true && isRefund == true)
            return query.Where(p => p.IsStorno || p.IsRefund);
        if (isStorno == true)
            return query.Where(p => p.IsStorno);
        if (isRefund == true)
            return query.Where(p => p.IsRefund);
        return query;
    }

    public static IQueryable<PaymentDetails> ApplySorting(
        this IQueryable<PaymentDetails> query,
        PaymentFilterDto filter)
    {
        var desc = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return filter.SortBy.ToLowerInvariant() switch
        {
            "amount" or "totalamount" => desc
                ? query.OrderByDescending(p => p.TotalAmount).ThenByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.TotalAmount).ThenByDescending(p => p.CreatedAt),
            "receiptnumber" => desc
                ? query.OrderByDescending(p => p.ReceiptNumber).ThenByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.ReceiptNumber).ThenByDescending(p => p.CreatedAt),
            "customername" => desc
                ? query.OrderByDescending(p => p.CustomerName).ThenByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CustomerName).ThenByDescending(p => p.CreatedAt),
            _ => desc
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
        };
    }

    public static FilterSummaryDto BuildFilterSummary(
        PaymentFilterDto filter,
        IReadOnlyList<string> availablePaymentMethods,
        bool usedDefaultDateWindow)
    {
        var applied = new Dictionary<string, object>();
        var count = 0;

        void Add<T>(string key, T? value) where T : class
        {
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                return;
            applied[key] = value;
            count++;
        }

        void AddValue<T>(string key, T? value) where T : struct
        {
            if (!value.HasValue)
                return;
            applied[key] = value.Value;
            count++;
        }

        if (!usedDefaultDateWindow)
        {
            if (filter.StartDate.HasValue)
                AddValue("startDate", filter.StartDate);
            if (filter.EndDate.HasValue)
                AddValue("endDate", filter.EndDate);
        }

        AddValue("minAmount", filter.MinAmount);
        AddValue("maxAmount", filter.MaxAmount);

        if (filter.PaymentMethods.Count > 0)
        {
            applied["paymentMethods"] = filter.PaymentMethods;
            count++;
        }

        if (filter.Statuses.Count > 0)
        {
            applied["statuses"] = filter.Statuses;
            count++;
        }

        AddValue("cashRegisterId", filter.CashRegisterId);
        Add("customerName", filter.CustomerName);
        Add("customerEmail", filter.CustomerEmail);
        Add("cashierId", filter.CashierId);
        Add("receiptNumber", filter.ReceiptNumber);
        AddValue("isStorno", filter.IsStorno);
        AddValue("isRefund", filter.IsRefund);

        return new FilterSummaryDto
        {
            ActiveFilterCount = count,
            AppliedFilters = applied,
            AvailablePaymentMethods = availablePaymentMethods.ToList(),
            AvailableStatuses = KnownStatuses.ToList(),
        };
    }

    /// <summary>Counts user-supplied filters (excludes default date window).</summary>
    public static int GetActiveFilterCount(PaymentFilterDto filter, bool usedDefaultDateWindow = false)
    {
        var count = 0;
        if (!usedDefaultDateWindow)
        {
            if (filter.StartDate.HasValue) count++;
            if (filter.EndDate.HasValue) count++;
        }
        if (filter.MinAmount.HasValue) count++;
        if (filter.MaxAmount.HasValue) count++;
        if (filter.PaymentMethods.Count > 0) count++;
        if (filter.Statuses.Count > 0) count++;
        if (filter.CashRegisterId.HasValue) count++;
        if (!string.IsNullOrEmpty(filter.CustomerName)) count++;
        if (!string.IsNullOrEmpty(filter.CustomerEmail)) count++;
        if (!string.IsNullOrEmpty(filter.CashierId)) count++;
        if (!string.IsNullOrEmpty(filter.ReceiptNumber)) count++;
        if (filter.IsStorno.HasValue) count++;
        if (filter.IsRefund.HasValue) count++;
        return count;
    }
}
