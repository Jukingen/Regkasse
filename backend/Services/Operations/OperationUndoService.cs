using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Operations;

public sealed class OperationUndoService : IOperationUndoService
{
    private readonly AppDbContext _db;
    private readonly IOperationLogService _operationLogs;
    private readonly IAuditLogService _auditLogService;
    private readonly IOptionsMonitor<GracePeriodsOptions> _graceOptions;
    private readonly ILogger<OperationUndoService> _logger;

    public OperationUndoService(
        AppDbContext db,
        IOperationLogService operationLogs,
        IAuditLogService auditLogService,
        IOptionsMonitor<GracePeriodsOptions> graceOptions,
        ILogger<OperationUndoService> logger)
    {
        _db = db;
        _operationLogs = operationLogs;
        _auditLogService = auditLogService;
        _graceOptions = graceOptions;
        _logger = logger;
    }

    public async Task<UndoOperationResponse> UndoOperationAsync(
        Guid tenantId,
        Guid operationId,
        string undoByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Fail("TENANT_REQUIRED", "Tenant context is required.");
        if (string.IsNullOrWhiteSpace(undoByUserId))
            return Fail("AUTH_REQUIRED", "Authentication required.");

        var operation = await _db.OperationLogs
            .FirstOrDefaultAsync(o => o.Id == operationId && o.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (operation is null)
            return Fail("NOT_FOUND", "Operation not found.");

        if (operation.IsUndone)
            return Fail("ALREADY_UNDONE", "Operation already undone.");

        if (!_operationLogs.IsUndoable(operation.OperationType))
        {
            return Fail(
                "NOT_UNDOABLE",
                operation.OperationType == OperationTypes.CreatePayment
                    ? "Payment operations cannot be undone (RKSV)."
                    : $"Operation {operation.OperationType} cannot be undone.");
        }

        if (!IsWithinGraceWindow(operation))
            return Fail("GRACE_EXPIRED", "The undo grace period for this operation has expired.");

        var restore = await RestoreStateAsync(operation, undoByUserId, cancellationToken).ConfigureAwait(false);
        if (!restore.Ok)
            return Fail(restore.ErrorCode!, restore.Message!);

        operation.IsUndone = true;
        operation.UndoneBy = undoByUserId;
        operation.UndoneAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
            operation.Reason = string.IsNullOrWhiteSpace(operation.Reason)
                ? reason.Trim()
                : $"{operation.Reason} | undo: {reason.Trim()}";

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                AuditLogActions.OPERATION_UNDONE,
                "OperationLog",
                undoByUserId,
                "Admin",
                description: $"Undid {operation.OperationType} on {operation.EntityType}/{operation.EntityId}",
                notes: reason,
                requestData: new
                {
                    OperationId = operationId,
                    UndoBy = undoByUserId,
                    operation.OperationType,
                    operation.EntityType,
                    operation.EntityId,
                },
                actionType: AuditEventType.OperationUndone,
                entityId: operationId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit log for operation undo {OperationId}", operationId);
        }

        return new UndoOperationResponse
        {
            Success = true,
            OperationId = operationId,
        };
    }

    private async Task<UndoResult> RestoreStateAsync(
        OperationLog operation,
        string undoByUserId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(operation.EntityId, out var entityId))
            return UndoResult.Fail("INVALID_ENTITY_ID", "Entity id is not a valid GUID.");

        return operation.OperationType switch
        {
            OperationTypes.UpdateProduct => await RestoreProductAsync(entityId, operation.BeforeState, cancellationToken)
                .ConfigureAwait(false),
            OperationTypes.UpdateCustomer => await RestoreCustomerAsync(entityId, operation.BeforeState, cancellationToken)
                .ConfigureAwait(false),
            OperationTypes.CreateCategory => await UndoCreateCategoryAsync(entityId, cancellationToken)
                .ConfigureAwait(false),
            OperationTypes.CreateVoucher => await UndoCreateVoucherAsync(operation, entityId, undoByUserId, cancellationToken)
                .ConfigureAwait(false),
            _ => UndoResult.Fail("NOT_UNDOABLE", $"Operation {operation.OperationType} cannot be undone."),
        };
    }

    private async Task<UndoResult> RestoreProductAsync(
        Guid entityId,
        string? beforeState,
        CancellationToken cancellationToken)
    {
        var snapshot = OperationSnapshots.Deserialize<ProductOperationSnapshot>(beforeState);
        if (snapshot is null)
            return UndoResult.Fail("MISSING_BEFORE_STATE", "Before state is missing; cannot restore product.");

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == entityId, cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
            return UndoResult.Fail("ENTITY_NOT_FOUND", "Product not found.");

        OperationSnapshots.ApplyProduct(product, snapshot);
        product.UpdatedAt = DateTime.UtcNow;
        return UndoResult.Success();
    }

    private async Task<UndoResult> RestoreCustomerAsync(
        Guid entityId,
        string? beforeState,
        CancellationToken cancellationToken)
    {
        var snapshot = OperationSnapshots.Deserialize<CustomerOperationSnapshot>(beforeState);
        if (snapshot is null)
            return UndoResult.Fail("MISSING_BEFORE_STATE", "Before state is missing; cannot restore customer.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == entityId, cancellationToken)
            .ConfigureAwait(false);
        if (customer is null)
            return UndoResult.Fail("ENTITY_NOT_FOUND", "Customer not found.");
        if (customer.IsSystem)
            return UndoResult.Fail("SYSTEM_CUSTOMER", "System customers cannot be restored via undo.");

        OperationSnapshots.ApplyCustomer(customer, snapshot);
        customer.UpdatedAt = DateTime.UtcNow;
        return UndoResult.Success();
    }

    private async Task<UndoResult> UndoCreateCategoryAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == entityId, cancellationToken)
            .ConfigureAwait(false);
        if (category is null)
            return UndoResult.Fail("ENTITY_NOT_FOUND", "Category not found.");
        if (category.IsSystemCategory)
            return UndoResult.Fail("SYSTEM_CATEGORY", "System categories cannot be undone.");

        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;
        return UndoResult.Success();
    }

    private async Task<UndoResult> UndoCreateVoucherAsync(
        OperationLog operation,
        Guid entityId,
        string undoByUserId,
        CancellationToken cancellationToken)
    {
        var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Id == entityId, cancellationToken)
            .ConfigureAwait(false);
        if (voucher is null)
            return UndoResult.Fail("ENTITY_NOT_FOUND", "Voucher not found.");

        if (voucher.Status is not VoucherStatus.Active)
            return UndoResult.Fail("VOUCHER_NOT_ACTIVE", "Only unused active vouchers can be undone.");

        if (voucher.RemainingAmount != voucher.InitialAmount)
            return UndoResult.Fail("VOUCHER_USED", "Voucher balance changed; cannot undo create.");

        var remaining = voucher.RemainingAmount;
        var utcNow = DateTime.UtcNow;
        voucher.RemainingAmount = 0;
        voucher.Status = VoucherStatus.Cancelled;
        voucher.CancelledAtUtc = utcNow;
        voucher.CancellationReason = "Undone via operation log";

        var cancelKey = $"op-undo-cancel:{voucher.Id:N}:{operation.Id:N}";
        _db.VoucherLedgerEntries.Add(new VoucherLedgerEntry
        {
            TenantId = voucher.TenantId,
            VoucherId = voucher.Id,
            Type = VoucherTransactionType.Cancel,
            Amount = -remaining,
            BalanceAfter = 0,
            CreatedByUserId = undoByUserId,
            CreatedAtUtc = utcNow,
            IdempotencyKey = cancelKey.Length <= 128 ? cancelKey : cancelKey[..128],
        });

        return UndoResult.Success();
    }

    private bool IsWithinGraceWindow(OperationLog operation)
    {
        var opts = _graceOptions.CurrentValue;
        if (!opts.Enabled)
            return true;

        var kind = operation.OperationType switch
        {
            OperationTypes.UpdateProduct => GracePeriodActionKinds.PriceUpdate,
            OperationTypes.CreateCategory or OperationTypes.CreateVoucher or OperationTypes.UpdateCustomer
                => GracePeriodActionKinds.BulkDelete,
            _ => null,
        };
        if (kind is null)
            return true;

        var rule = opts.Resolve(kind);
        if (rule is null || rule.Duration <= TimeSpan.Zero)
            return true;

        var created = operation.CreatedAt.Kind == DateTimeKind.Utc
            ? operation.CreatedAt
            : DateTime.SpecifyKind(operation.CreatedAt, DateTimeKind.Utc);
        return DateTime.UtcNow <= created.Add(rule.Duration);
    }

    private static UndoOperationResponse Fail(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        Message = message,
    };
}
