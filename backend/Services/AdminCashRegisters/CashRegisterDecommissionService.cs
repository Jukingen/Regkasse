using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.AdminCashRegisters;

/// <summary>
/// Admin cash register decommission (RKSV Schlussbeleg) and dev-only hard delete.
/// </summary>
public sealed class CashRegisterDecommissionService : ICashRegisterDecommissionService
{
    public const string HardDeleteConfirmPhrase = "HARD_DELETE";

    private readonly AppDbContext _db;
    private readonly IRksvSpecialReceiptService _rksvSpecialReceipts;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IAuditLogService _auditLog;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<CashRegisterComplianceOptions> _options;
    private readonly ILogger<CashRegisterDecommissionService> _logger;

    public CashRegisterDecommissionService(
        AppDbContext db,
        IRksvSpecialReceiptService rksvSpecialReceipts,
        ISettingsTenantResolver tenantResolver,
        IAuditLogService auditLog,
        IHostEnvironment environment,
        IOptions<CashRegisterComplianceOptions> options,
        ILogger<CashRegisterDecommissionService> logger)
    {
        _db = db;
        _rksvSpecialReceipts = rksvSpecialReceipts;
        _tenantResolver = tenantResolver;
        _auditLog = auditLog;
        _environment = environment;
        _options = options;
        _logger = logger;
    }

    public bool IsHardDeleteAllowed() =>
        _environment.IsDevelopment() && _options.Value.AllowHardDelete;

    public async Task<DecommissionCashRegisterResponse> DecommissionAsync(
        Guid cashRegisterId,
        string? reason,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var registerBefore = await _db.CashRegisters.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        var priorStatus = registerBefore.Status.ToString();

        var result = await _rksvSpecialReceipts.CreateSchlussbelegAsync(
            new CreateSchlussbelegRequest
            {
                CashRegisterId = cashRegisterId,
                Reason = reason,
            },
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        await TryAuditDecommissionAsync(
            cashRegisterId,
            registerBefore.RegisterNumber,
            priorStatus,
            reason,
            result,
            actorUserId,
            actorRole).ConfigureAwait(false);

        return new DecommissionCashRegisterResponse
        {
            CashRegisterId = cashRegisterId,
            PaymentId = result.PaymentId,
            ReceiptId = result.ReceiptId,
            ReceiptNumber = result.ReceiptNumber,
            Message =
                "Schlussbeleg (Endbeleg) created; cash register status set to Decommissioned. No new payments allowed.",
        };
    }

    public async Task HardDeleteAsync(
        Guid cashRegisterId,
        string confirmPhrase,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (!IsHardDeleteAllowed())
        {
            throw new InvalidOperationException(
                "Hard delete is disabled. Set CashRegister:AllowHardDelete=true and run in Development only.");
        }

        if (!string.Equals(confirmPhrase?.Trim(), HardDeleteConfirmPhrase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Confirmation phrase must be exactly «{HardDeleteConfirmPhrase}».");
        }

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var register = await _db.CashRegisters
            .FirstOrDefaultAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Cash register not found for the current tenant.");

        var paymentCount = await _db.PaymentDetails.AsNoTracking()
            .CountAsync(p => p.CashRegisterId == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (paymentCount > 0)
        {
            throw new InvalidOperationException(
                "Cannot hard-delete a register with payment/receipt rows. Use only on empty test registers.");
        }

        var transactions = await _db.CashRegisterTransactions
            .Where(t => t.CashRegisterId == cashRegisterId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (transactions.Count > 0)
            _db.CashRegisterTransactions.RemoveRange(transactions);

        var snapshot = new
        {
            register.RegisterNumber,
            register.Location,
            register.Status,
        };

        _db.CashRegisters.Remove(register);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryAuditHardDeleteAsync(cashRegisterId, snapshot, actorUserId, actorRole).ConfigureAwait(false);

        _logger.LogWarning(
            "Cash register hard-deleted (dev/test only) RegisterId={RegisterId} RegisterNumber={RegisterNumber} Actor={Actor}",
            cashRegisterId,
            register.RegisterNumber,
            actorUserId);
    }

    private async Task TryAuditDecommissionAsync(
        Guid cashRegisterId,
        string registerNumber,
        string priorStatus,
        string? reason,
        CreateSchlussbelegResponse schlussbeleg,
        string actorUserId,
        string actorRole)
    {
        try
        {
            await _auditLog.LogEntityChangeAsync(
                AuditLogActions.CASH_REGISTER_DECOMMISSION,
                AuditLogEntityTypes.CASH_REGISTER,
                cashRegisterId,
                actorUserId,
                actorRole,
                oldValues: new { status = priorStatus, registerNumber },
                newValues: new
                {
                    status = RegisterStatus.Decommissioned.ToString(),
                    registerNumber,
                    schlussbeleg.ReceiptNumber,
                    schlussbeleg.PaymentId,
                    schlussbeleg.ReceiptId,
                    decommissionReason = reason,
                },
                description:
                    $"Cash register {registerNumber} decommissioned via RKSV Schlussbeleg (Endbeleg).",
                notes: string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Audit log failed for cash register decommission RegisterId={RegisterId}",
                cashRegisterId);
        }
    }

    private async Task TryAuditHardDeleteAsync(
        Guid cashRegisterId,
        object snapshot,
        string actorUserId,
        string actorRole)
    {
        try
        {
            await _auditLog.LogEntityChangeAsync(
                AuditLogActions.CASH_REGISTER_HARD_DELETE,
                AuditLogEntityTypes.CASH_REGISTER,
                cashRegisterId,
                actorUserId,
                actorRole,
                oldValues: snapshot,
                newValues: null,
                description: "Cash register row hard-deleted (development/test only).",
                notes: "NUR FÜR TESTUMGEBUNGEN").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for cash register hard delete RegisterId={RegisterId}", cashRegisterId);
        }
    }
}
