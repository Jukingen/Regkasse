using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin visibility for card acquirer intents/charges (Mock or Stripe).</summary>
[Authorize]
[ApiController]
[Route("api/admin/card-transactions")]
[Produces("application/json")]
[HasPermission(AppPermissions.PaymentView)]
public class AdminCardTransactionsController : ControllerBase
{
    private readonly IAdminCardTransactionListService _listService;
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminCardTransactionsController(
        IAdminCardTransactionListService listService,
        AppDbContext context,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _listService = listService;
        _context = context;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet]
    public async Task<ActionResult<AdminCardTransactionListResponse>> List(
        [FromQuery] AdminCardTransactionFilterDto filter,
        CancellationToken cancellationToken)
    {
        var response = await _listService.QueryAsync(filter, cancellationToken).ConfigureAwait(false);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminCardTransactionListItemDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var row = await _context.CardPaymentTransactions.AsNoTracking()
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .Select(c => new AdminCardTransactionListItemDto
            {
                Id = c.Id,
                Amount = c.Amount,
                Currency = c.Currency,
                Status = c.Status,
                GatewayProvider = c.Gateway,
                TransactionId = c.GatewayTransactionId,
                CardBrand = c.CardBrand,
                LastFourDigits = c.CardLast4,
                ErrorMessage = c.ErrorMessage,
                CashRegisterId = c.CashRegisterId,
                PaymentDetailsId = c.PaymentId,
                CreatedAtUtc = c.CreatedAt,
                ConfirmedAtUtc = c.CompletedAt,
                RefundedAmount = c.RefundedAmount,
                CashRegisterLabel = _context.CashRegisters.AsNoTracking()
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
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row == null)
            return NotFound();

        return Ok(row);
    }
}
