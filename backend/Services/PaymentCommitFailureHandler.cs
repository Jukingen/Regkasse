using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services;

/// <summary>
/// Maps a fiscal-commit exception after the caller has rolled the transaction back and cleared the tracker.
/// Reads the existing payment for an idempotency replay. Does not start a transaction or save.
/// </summary>
public interface IPaymentCommitFailureHandler
{
    Task<FiscalCommitFailure> MapFiscalCommitFailureAsync(
        Exception ex,
        string? idempotencyKey,
        Guid customerId);
}

/// <summary>Either a payment result to return, or a signal to rethrow the original commit exception.</summary>
public sealed class FiscalCommitFailure
{
    public PaymentResult? Result { get; init; }
    public bool Rethrow { get; init; }

    public static FiscalCommitFailure Return(PaymentResult result) => new() { Result = result };

    public static FiscalCommitFailure Throw() => new() { Rethrow = true };
}

public sealed class PaymentCommitFailureHandler : IPaymentCommitFailureHandler
{
    private readonly AppDbContext _context;
    private readonly ILogger _logger;
    private readonly PaymentQrPayloadBuilder _buildQrPayload;

    public PaymentCommitFailureHandler(
        AppDbContext context,
        ILogger<PaymentService> logger,
        PaymentQrPayloadBuilder buildQrPayload)
    {
        _context = context;
        _logger = logger;
        _buildQrPayload = buildQrPayload;
    }

    /// <inheritdoc />
    public async Task<FiscalCommitFailure> MapFiscalCommitFailureAsync(
        Exception ex,
        string? idempotencyKey,
        Guid customerId)
    {
        if (ex is DbUpdateConcurrencyException concurrency)
        {
            _logger.LogWarning(concurrency, "Daily allowance concurrency conflict for customer {CustomerId} during payment creation", customerId);
            return FiscalCommitFailure.Return(PaymentCommitFailureMapper.ToDailyAllowanceConflictResult());
        }

        if (ex is DbUpdateException idempotencyEx && PaymentCommitFailureMapper.IsIdempotencyKeyViolation(idempotencyEx))
        {
            if (idempotencyKey != null)
            {
                var existing = await _context.PaymentDetails.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
                if (existing != null)
                {
                    var invoiceExists = await _context.Invoices.AsNoTracking().AnyAsync(i => i.SourcePaymentId == existing.Id);
                    _logger.LogInformation("Idempotency key race: returning existing payment {PaymentId} for key {Key}", existing.Id, idempotencyKey);
                    var (qr, demo, provider) = await _buildQrPayload(existing, !string.IsNullOrEmpty(existing.TseSignature));
                    return FiscalCommitFailure.Return(new PaymentResult
                    {
                        Success = true,
                        Message = "Payment created successfully",
                        Payment = existing,
                        PaymentId = existing.Id,
                        TseSignature = existing.TseSignature,
                        QrPayload = qr,
                        IsDemoFiscal = demo,
                        TseProvider = provider,
                        InvoicePersisted = invoiceExists,
                        IdempotentReplay = true,
                        TimeSyncWarning = existing.TimeSyncWarning
                    });
                }
            }

            if (PaymentCommitFailureMapper.IsVoucherLedgerIdempotencyConstraintViolation(idempotencyEx))
            {
                const string msg = "Voucher already used in this transaction. Please retry with a new idempotency key.";
                _logger.LogWarning(idempotencyEx, "Voucher ledger idempotency unique constraint violated during payment create");
                return FiscalCommitFailure.Return(new PaymentResult
                {
                    Success = false,
                    Message = msg,
                    Errors = { msg },
                    IsDeterministicFailure = true,
                    DiagnosticCode = "VOUCHER_LEDGER_IDEMPOTENCY_CONFLICT"
                });
            }

            if (PaymentCommitFailureMapper.TryGetPostgresException(idempotencyEx, out var pgExIdem))
            {
                _logger.LogError(
                    pgExIdem,
                    "PostgreSQL violation (idempotency catch path): SqlState={SqlState}, Constraint={Constraint}, Detail={Detail}",
                    pgExIdem.SqlState,
                    pgExIdem.ConstraintName,
                    pgExIdem.Detail);
                if (PaymentCommitFailureMapper.TryMapPaymentCommitPostgresException(pgExIdem, out var pgMappedIdem))
                    return FiscalCommitFailure.Return(pgMappedIdem);
            }

            _logger.LogError(idempotencyEx, "Fiscal transaction failed (idempotency key violation) for payment");
            return FiscalCommitFailure.Throw();
        }

        if (ex is DbUpdateException benefitEx && PaymentCommitFailureMapper.IsBenefitDailyUsageConflict(benefitEx))
        {
            _logger.LogWarning(benefitEx, "Daily allowance unique constraint race for customer {CustomerId} during payment creation", customerId);
            return FiscalCommitFailure.Return(PaymentCommitFailureMapper.ToDailyAllowanceConflictResult());
        }

        if (ex is DbUpdateException saveEx)
        {
            if (PaymentCommitFailureMapper.TryGetPostgresException(saveEx, out var pgExSave))
            {
                _logger.LogError(
                    pgExSave,
                    "PostgreSQL violation (payment commit): SqlState={SqlState}, Constraint={Constraint}, Detail={Detail}",
                    pgExSave.SqlState,
                    pgExSave.ConstraintName,
                    pgExSave.Detail);
                if (PaymentCommitFailureMapper.TryMapPaymentCommitPostgresException(pgExSave, out var pgMappedSave))
                    return FiscalCommitFailure.Return(pgMappedSave);
            }

            _logger.LogError(saveEx, "Fiscal transaction failed for payment; DbUpdateException after rollback");
            return FiscalCommitFailure.Throw();
        }

        if (ex is ExtensionFailedException extensionEx)
        {
            _logger.LogWarning(
                extensionEx,
                "Tenant extension {Feature} rejected payment before commit",
                extensionEx.FeatureName);
            return FiscalCommitFailure.Return(PaymentService.ExtensionRejected(extensionEx));
        }

        _logger.LogError(ex, "Fiscal transaction failed for payment; Payment, Invoice, Receipt, and stock updates rolled back");
        return FiscalCommitFailure.Throw();
    }
}
