using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IAdminCardTransactionListService
{
    Task<AdminCardTransactionListResponse> QueryAsync(
        AdminCardTransactionFilterDto filter,
        CancellationToken cancellationToken = default);
}

public sealed class AdminCardTransactionListService : IAdminCardTransactionListService
{
    private const int MaxPageSize = 200;

    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminCardTransactionListService(
        AppDbContext context,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _settingsTenantResolver = settingsTenantResolver;
    }

    public async Task<AdminCardTransactionListResponse> QueryAsync(
        AdminCardTransactionFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 50 : Math.Min(filter.PageSize, MaxPageSize);

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<CardPaymentTransaction> query = _context.CardPaymentTransactions.AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        if (filter.FromUtc.HasValue)
            query = query.Where(c => c.CreatedAt >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue)
            query = query.Where(c => c.CreatedAt <= filter.ToUtc.Value);
        if (filter.CashRegisterId.HasValue && filter.CashRegisterId.Value != Guid.Empty)
            query = query.Where(c => c.CashRegisterId == filter.CashRegisterId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(c => c.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Amount,
                c.Currency,
                c.Status,
                c.Gateway,
                c.GatewayTransactionId,
                c.CardBrand,
                c.CardLast4,
                c.ErrorMessage,
                c.CashRegisterId,
                c.PaymentId,
                c.CreatedAt,
                c.CompletedAt,
                c.RefundedAmount,
                RegisterNumber = _context.CashRegisters.AsNoTracking()
                    .Where(r => r.Id == c.CashRegisterId)
                    .Select(r => r.RegisterNumber)
                    .FirstOrDefault(),
                ReceiptNumber = c.PaymentId.HasValue
                    ? _context.PaymentDetails.AsNoTracking()
                        .Where(p => p.Id == c.PaymentId)
                        .Select(p => p.ReceiptNumber)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminCardTransactionListResponse
        {
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = rows.Select(r => new AdminCardTransactionListItemDto
            {
                Id = r.Id,
                Amount = r.Amount,
                Currency = r.Currency,
                Status = r.Status,
                GatewayProvider = r.Gateway,
                TransactionId = r.GatewayTransactionId,
                CardBrand = r.CardBrand,
                LastFourDigits = r.CardLast4,
                ErrorMessage = r.ErrorMessage,
                CashRegisterId = r.CashRegisterId,
                CashRegisterLabel = r.RegisterNumber,
                PaymentDetailsId = r.PaymentId,
                ReceiptNumber = r.ReceiptNumber,
                CreatedAtUtc = r.CreatedAt,
                ConfirmedAtUtc = r.CompletedAt,
                RefundedAmount = r.RefundedAmount
            }).ToList()
        };
    }
}
