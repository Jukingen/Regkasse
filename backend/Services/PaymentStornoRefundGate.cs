using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Admission for a full storno or partial refund on the payment-create path.
/// Runs after the customer is known and before the fiscal transaction. Does not begin a transaction or save.
/// Null means the request is a sale and the caller should continue.
/// </summary>
public interface IPaymentStornoRefundGate
{
    Task<PaymentResult?> TryRedirectStornoOrRefundAsync(
        CreatePaymentRequest request,
        string userId,
        Customer customer);
}

public sealed class PaymentStornoRefundGate : IPaymentStornoRefundGate
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;
    private readonly ICashRegisterResolutionService _cashRegisterResolution;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPaymentStornoService _storno;
    private readonly IPaymentRefundService _refund;
    private readonly Func<PaymentDetails, CancellationToken, Task<bool>> _paymentBelongsToTenant;

    public PaymentStornoRefundGate(
        AppDbContext context,
        ILogger<PaymentService> logger,
        ICashRegisterResolutionService cashRegisterResolution,
        IHttpContextAccessor httpContextAccessor,
        IPaymentStornoService storno,
        IPaymentRefundService refund,
        Func<PaymentDetails, CancellationToken, Task<bool>> paymentBelongsToTenant)
    {
        _context = context;
        _logger = logger;
        _cashRegisterResolution = cashRegisterResolution;
        _httpContextAccessor = httpContextAccessor;
        _storno = storno;
        _refund = refund;
        _paymentBelongsToTenant = paymentBelongsToTenant;
    }

    /// <inheritdoc />
    public async Task<PaymentResult?> TryRedirectStornoOrRefundAsync(
        CreatePaymentRequest request,
        string userId,
        Customer customer)
    {
        if (!request.IsStorno && !request.IsRefund)
            return null;

        if (request.IsStorno && request.IsRefund)
        {
            return new PaymentResult
            {
                Success = false,
                Message = "Storno and refund are mutually exclusive",
                Errors = { "IsStorno and IsRefund cannot both be true." },
                IsDeterministicFailure = true
            };
        }

        if (request.CashRegisterId == Guid.Empty)
        {
            return new PaymentResult
            {
                Success = false,
                Message = "CashRegisterId is required",
                Errors = { "CashRegisterId must be a non-empty GUID referencing an authorized open cash register." },
                DiagnosticCode = CashRegisterResolutionCodes.Required,
                IsDeterministicFailure = true
            };
        }

        var principalStornoRefund = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var registerValidationSr = await _cashRegisterResolution.ValidatePaymentRegisterAsync(
            userId,
            request.CashRegisterId,
            principalStornoRefund);
        if (!registerValidationSr.Ok)
        {
            _logger.LogWarning(
                "Storno/refund payment create rejected: cash register policy. UserId={UserId} RegisterId={RegisterId} Code={Code}",
                userId,
                request.CashRegisterId,
                registerValidationSr.Code);
            return new PaymentResult
            {
                Success = false,
                Message = registerValidationSr.Message,
                Errors = { registerValidationSr.Message },
                DiagnosticCode = MapCashRegisterDiagnosticToRksvPaymentCode(registerValidationSr.Code)
            };
        }

        var registerIdResolved = registerValidationSr.ResolvedRegisterId!.Value;
        var belegNr = request.OriginalReceiptNumber!.Trim();
        var originalSale = await _storno.FindOriginalSalePaymentByReceiptNumberAsync(registerIdResolved, belegNr);
        if (originalSale == null)
        {
            return new PaymentResult
            {
                Success = false,
                Message = "Original receipt not found",
                Errors = { "No active sale payment matches OriginalReceiptNumber for this cash register." },
                IsDeterministicFailure = true
            };
        }

        if (!await _paymentBelongsToTenant(originalSale, CancellationToken.None))
        {
            return new PaymentResult
            {
                Success = false,
                Message = "Original receipt not found",
                Errors = { "No active sale payment matches OriginalReceiptNumber for this cash register." },
                IsDeterministicFailure = true
            };
        }

        if (originalSale.CustomerId != request.CustomerId)
        {
            return new PaymentResult
            {
                Success = false,
                Message = "Customer does not match original receipt",
                Errors = { "CustomerId must match the original sale for storno or refund." },
                IsDeterministicFailure = true
            };
        }

        const decimal parityTol = 0.01m;
        if (request.IsStorno)
        {
            if (Math.Abs(request.TotalAmount - originalSale.TotalAmount) > parityTol)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Storno total does not match original receipt",
                    Errors = { "TotalAmount must match the original sale gross total for a full storno." },
                    IsDeterministicFailure = true
                };
            }

            var refundRowsExist = await _context.PaymentDetails.AsNoTracking()
                .AnyAsync(p => p.OriginalPaymentId == originalSale.Id && p.IsRefund && p.IsActive);
            if (refundRowsExist)
            {
                return new PaymentResult
                {
                    Success = false,
                    Message = "Cannot storno: partial refunds already exist for this receipt",
                    Errors = { "A full storno is not allowed after partial refunds on the same sale." },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "STORNO_BLOCKED_BY_REFUNDS"
                };
            }

            var auditReason = _storno.FormatStornoReasonForAudit(request.StornoReason!.Value);
            return await _storno.CreateStornoReversalAsync(
                originalSale,
                auditReason,
                userId,
                string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
                request.StornoReason);
        }

        var refundNotes = string.IsNullOrWhiteSpace(request.Notes) ? "Refund (POS create)" : request.Notes.Trim();
        return await _refund.RefundPaymentAsync(
            originalSale.Id,
            decimal.Round(request.TotalAmount, 2, MidpointRounding.AwayFromZero),
            refundNotes,
            userId,
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim());
    }

    private static string? MapCashRegisterDiagnosticToRksvPaymentCode(string? cashRegisterResolutionCode) =>
        string.Equals(cashRegisterResolutionCode, CashRegisterResolutionCodes.Decommissioned, StringComparison.Ordinal)
            ? RksvGuardErrorCodes.RegisterDecommissioned
            : cashRegisterResolutionCode;
}
