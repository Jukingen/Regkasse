using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Offline;

public sealed class OfflineOrderService : IOfflineOrderService
{
    private const int ExpiryHours = 72;
    private const int MaxSyncAttempts = 3;
    private const int SequenceReservationAttempts = 3;

    private readonly AppDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly ISequenceReservationService _sequenceReservation;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<OfflineOrderService> _logger;

    public OfflineOrderService(
        AppDbContext context,
        IPaymentService paymentService,
        ISequenceReservationService sequenceReservation,
        IAuditLogService auditLogService,
        ICurrentTenantAccessor tenantAccessor,
        IHttpContextAccessor httpContextAccessor,
        ILogger<OfflineOrderService> logger)
    {
        _context = context;
        _paymentService = paymentService;
        _sequenceReservation = sequenceReservation;
        _auditLogService = auditLogService;
        _tenantAccessor = tenantAccessor;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<OfflineOrderResponse> SaveOfflineOrderAsync(
        OfflineOrderRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenantId();
        ValidatePaymentMethodAllowed(request.PaymentMethod);
        await EnsureCashRegisterExistsAsync(request.CashRegisterId, tenantId, ct).ConfigureAwait(false);

        var userId = ResolveActorUserIdOrSystem();
        var userRole = ResolveActorRoleOrSystem();

        var now = DateTime.UtcNow;
        var order = new OfflineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = request.CashRegisterId,
            OfflineOrderId = GenerateOfflineOrderId(now),
            OrderData = SerializeOrderData(request.OrderData),
            OrderTotal = request.OrderTotal,
            PaymentMethod = request.PaymentMethod.Trim(),
            Status = OfflineOrderStatuses.Pending,
            SyncAttempts = 0,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(ExpiryHours)
        };

        _context.OfflineOrders.Add(order);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        await _auditLogService.LogSystemOperationAsync(
            action: "OFFLINE_ORDER_CREATED",
            entityType: "OfflineOrder",
            userId: userId,
            userRole: userRole,
            description: $"Offline order {order.OfflineOrderId} queued for cash register {order.CashRegisterId}.",
            requestData: new
            {
                order.OfflineOrderId,
                order.CashRegisterId,
                order.OrderTotal,
                order.PaymentMethod,
                order.ExpiresAtUtc
            },
            entityId: order.Id,
            tenantId: tenantId).ConfigureAwait(false);

        _logger.LogInformation(
            "Offline order saved. OfflineOrderId={OfflineOrderId} CashRegisterId={CashRegisterId} TenantId={TenantId}",
            order.OfflineOrderId, order.CashRegisterId, order.TenantId);

        return ToResponse(order);
    }

    public async Task<List<OfflineOrderResponse>> GetPendingOrdersAsync(
        Guid cashRegisterId,
        CancellationToken ct = default)
    {
        RequireTenantId();
        var now = DateTime.UtcNow;

        var orders = await _context.OfflineOrders
            .AsNoTracking()
            .Where(o => o.CashRegisterId == cashRegisterId
                        && o.Status == OfflineOrderStatuses.Pending
                        && o.ExpiresAtUtc > now)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return orders.Select(ToResponse).ToList();
    }

    public async Task<ReplayOfflineOrdersResult> ReplayPendingOrdersAsync(
        Guid cashRegisterId,
        CancellationToken ct = default)
    {
        RequireTenantId();
        var userId = ResolveActorUserId();
        var now = DateTime.UtcNow;

        var orders = await _context.OfflineOrders
            .Where(o => o.CashRegisterId == cashRegisterId
                        && o.Status == OfflineOrderStatuses.Pending
                        && o.ExpiresAtUtc > now)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (orders.Count == 0)
        {
            return new ReplayOfflineOrdersResult
            {
                Total = 0,
                Success = 0,
                Failed = 0,
                Details = new List<ReplayOfflineOrderResult>()
            };
        }

        var sequences = await _sequenceReservation
            .ReserveSequencesAsync(orders.Count, cashRegisterId, ct)
            .ConfigureAwait(false);

        var details = new List<ReplayOfflineOrderResult>(orders.Count);
        var sequencesToRelease = new List<int>();
        var success = 0;
        var failed = 0;

        try
        {
            for (var i = 0; i < orders.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var belegNr = await _sequenceReservation
                    .ToBelegNrAsync(cashRegisterId, sequences[i], ct)
                    .ConfigureAwait(false);

                var result = await ReplaySingleOrderAsync(orders[i], userId, ct, belegNr)
                    .ConfigureAwait(false);

                details.Add(result);
                if (result.Success)
                {
                    success++;
                }
                else
                {
                    failed++;
                    sequencesToRelease.Add(sequences[i]);
                }
            }
        }
        finally
        {
            if (sequencesToRelease.Count > 0)
            {
                await _sequenceReservation
                    .ReleaseSequencesAsync(sequencesToRelease, cashRegisterId, ct)
                    .ConfigureAwait(false);
            }
        }

        return new ReplayOfflineOrdersResult
        {
            Total = orders.Count,
            Success = success,
            Failed = failed,
            Details = details
        };
    }

    public async Task<int> CleanupExpiredOrdersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expired = await _context.OfflineOrders
            .Where(o => o.Status == OfflineOrderStatuses.Pending && o.ExpiresAtUtc <= now)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (expired.Count == 0)
            return 0;

        var summary = expired
            .Select(o => new { o.Id, o.OfflineOrderId, o.TenantId, o.CashRegisterId, o.ExpiresAtUtc })
            .ToList();

        _context.OfflineOrders.RemoveRange(expired);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        await _auditLogService.LogSystemOperationAsync(
            action: "OFFLINE_ORDERS_CLEANUP",
            entityType: "OfflineOrder",
            userId: "system",
            userRole: "System",
            description: $"Deleted {expired.Count} expired pending offline order(s).",
            requestData: summary,
            status: AuditLogStatus.Success).ConfigureAwait(false);

        _logger.LogInformation("Deleted {Count} expired pending offline orders.", expired.Count);
        return expired.Count;
    }

    public async Task<OfflineOrderResponse> GetOrderStatusAsync(
        string offlineOrderId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(offlineOrderId))
            throw new ArgumentException("Offline order id is required.", nameof(offlineOrderId));

        RequireTenantId();

        var order = await _context.OfflineOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OfflineOrderId == offlineOrderId, ct)
            .ConfigureAwait(false);

        if (order == null)
            throw new KeyNotFoundException($"Offline order '{offlineOrderId}' was not found.");

        return ToResponse(order);
    }

    public async Task<AdminOfflineOrdersListResponse> ListOrdersForAdminAsync(
        AdminOfflineOrdersListQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tenantId = RequireTenantId();
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var joined = from o in _context.OfflineOrders.AsNoTracking()
            join cr in _context.CashRegisters.AsNoTracking() on o.CashRegisterId equals cr.Id
            where o.TenantId == tenantId && cr.TenantId == tenantId
            select new { Order = o, Register = cr };

        if (query.CashRegisterId.HasValue && query.CashRegisterId.Value != Guid.Empty)
            joined = joined.Where(x => x.Order.CashRegisterId == query.CashRegisterId.Value);

        var status = query.Status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(status) && status != "all")
            joined = joined.Where(x => x.Order.Status == status);

        var total = await joined.CountAsync(ct).ConfigureAwait(false);
        var rows = await joined
            .OrderByDescending(x => x.Order.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var items = rows.Select(x =>
        {
            var remaining = x.Order.ExpiresAtUtc - now;
            var hoursRemaining = remaining.TotalHours <= 0 ? 0 : (int)Math.Ceiling(remaining.TotalHours);
            return new AdminOfflineOrderRowDto
            {
                Id = x.Order.Id,
                OfflineOrderId = x.Order.OfflineOrderId,
                CreatedAtUtc = x.Order.CreatedAtUtc,
                OrderTotal = x.Order.OrderTotal,
                PaymentMethod = x.Order.PaymentMethod,
                CashRegisterId = x.Order.CashRegisterId,
                CashRegisterLabel = $"{x.Register.RegisterNumber} · {x.Register.Location}",
                Status = x.Order.Status,
                HoursRemaining = hoursRemaining,
                SyncAttempts = x.Order.SyncAttempts,
                ErrorMessage = x.Order.ErrorMessage,
                SyncedPaymentId = x.Order.SyncedPaymentId,
                SyncedInvoiceNumber = x.Order.SyncedInvoiceNumber
            };
        }).ToList();

        return new AdminOfflineOrdersListResponse
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ReplayOfflineOrderResult> ReplayOrderByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        RequireTenantId();
        var userId = ResolveActorUserId();

        var order = await _context.OfflineOrders
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            .ConfigureAwait(false);

        if (order == null)
            throw new KeyNotFoundException($"Offline order '{orderId}' was not found.");

        if (order.Status != OfflineOrderStatuses.Pending)
            throw new InvalidOperationException("Only pending offline orders can be replayed.");

        if (order.ExpiresAtUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("Offline order has expired.");

        var sequences = await _sequenceReservation
            .ReserveSequencesAsync(1, order.CashRegisterId, ct)
            .ConfigureAwait(false);

        try
        {
            var belegNr = await _sequenceReservation
                .ToBelegNrAsync(order.CashRegisterId, sequences[0], ct)
                .ConfigureAwait(false);

            var result = await ReplaySingleOrderAsync(order, userId, ct, belegNr).ConfigureAwait(false);
            if (!result.Success)
            {
                await _sequenceReservation
                    .ReleaseSequencesAsync(sequences, order.CashRegisterId, ct)
                    .ConfigureAwait(false);
            }

            return result;
        }
        catch
        {
            await _sequenceReservation
                .ReleaseSequencesAsync(sequences, order.CashRegisterId, ct)
                .ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ReplayOfflineOrdersResult> ReplayAllPendingForTenantAsync(
        Guid? cashRegisterId = null,
        CancellationToken ct = default)
    {
        RequireTenantId();

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
            return await ReplayPendingOrdersAsync(cashRegisterId.Value, ct).ConfigureAwait(false);

        var registerIds = await _context.OfflineOrders
            .AsNoTracking()
            .Where(o => o.Status == OfflineOrderStatuses.Pending && o.ExpiresAtUtc > DateTime.UtcNow)
            .Select(o => o.CashRegisterId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var allDetails = new List<ReplayOfflineOrderResult>();
        var success = 0;
        var failed = 0;

        foreach (var registerId in registerIds)
        {
            ct.ThrowIfCancellationRequested();
            var batch = await ReplayPendingOrdersAsync(registerId, ct).ConfigureAwait(false);
            allDetails.AddRange(batch.Details);
            success += batch.Success;
            failed += batch.Failed;
        }

        return new ReplayOfflineOrdersResult
        {
            Total = allDetails.Count,
            Success = success,
            Failed = failed,
            Details = allDetails
        };
    }

    private async Task<ReplayOfflineOrderResult> ReplaySingleOrderAsync(
        OfflineOrder order,
        string userId,
        CancellationToken ct,
        string? reservedReceiptNumber = null)
    {
        order.SyncAttempts++;
        order.LastSyncAttemptUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            var paymentRequest = TryParsePaymentRequest(order.OrderData);
            if (paymentRequest == null)
                throw new InvalidOperationException("Order data could not be deserialized into a payment request.");

            if (paymentRequest.CashRegisterId != order.CashRegisterId)
                throw new InvalidOperationException("Order cash register does not match stored offline order.");

            if (string.IsNullOrWhiteSpace(paymentRequest.IdempotencyKey))
                paymentRequest.IdempotencyKey = $"offline-order:{order.Id:N}";

            PaymentResult? paymentResult = null;

            if (!string.IsNullOrWhiteSpace(reservedReceiptNumber))
            {
                paymentRequest.ReservedReceiptNumber = reservedReceiptNumber;
                paymentResult = await _paymentService.CreatePaymentAsync(
                    paymentRequest,
                    userId).ConfigureAwait(false);
            }
            else
            {
                for (var seqAttempt = 1; seqAttempt <= SequenceReservationAttempts; seqAttempt++)
                {
                    paymentRequest.ReservedReceiptNumber = await _sequenceReservation
                        .ReserveNextReceiptNumberAsync(order.CashRegisterId, ct)
                        .ConfigureAwait(false);

                    paymentResult = await _paymentService.CreatePaymentAsync(
                        paymentRequest,
                        userId).ConfigureAwait(false);

                    if (paymentResult.Success || !IsReceiptNumberConflict(paymentResult))
                        break;

                    _logger.LogWarning(
                        "Receipt number conflict during offline order replay. OrderId={OrderId} Attempt={Attempt}",
                        order.Id, seqAttempt);
                }
            }

            if (paymentResult == null)
                throw new InvalidOperationException("Offline order replay did not produce a payment result.");

            order = await _context.OfflineOrders
                .FirstAsync(o => o.Id == order.Id, ct)
                .ConfigureAwait(false);

            if (paymentResult.Success && paymentResult.PaymentId.HasValue)
            {
                var payment = paymentResult.Payment
                    ?? await _paymentService.GetPaymentAsync(paymentResult.PaymentId.Value).ConfigureAwait(false);

                order.Status = OfflineOrderStatuses.Synced;
                order.SyncedPaymentId = paymentResult.PaymentId.Value;
                order.SyncedInvoiceNumber = payment?.ReceiptNumber;
                order.SyncedAtUtc = DateTime.UtcNow;
                order.ErrorMessage = null;
                await _context.SaveChangesAsync(ct).ConfigureAwait(false);

                return new ReplayOfflineOrderResult
                {
                    OrderId = order.Id,
                    Success = true,
                    PaymentId = paymentResult.PaymentId.Value.ToString(),
                    InvoiceNumber = order.SyncedInvoiceNumber
                };
            }

            var errorMessage = paymentResult.Message;
            if (paymentResult.Errors.Count > 0)
                errorMessage = string.Join("; ", paymentResult.Errors);

            var isFinalFailure = order.SyncAttempts >= MaxSyncAttempts || paymentResult.IsDeterministicFailure;
            order.Status = isFinalFailure ? OfflineOrderStatuses.Failed : OfflineOrderStatuses.Pending;
            order.ErrorMessage = TruncateError(errorMessage);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);

            return new ReplayOfflineOrderResult
            {
                OrderId = order.Id,
                Success = false,
                ErrorMessage = order.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Offline order replay failed. OrderId={OrderId}", order.Id);

            order = await _context.OfflineOrders
                .FirstAsync(o => o.Id == order.Id, ct)
                .ConfigureAwait(false);

            var isFinalFailure = order.SyncAttempts >= MaxSyncAttempts;
            order.Status = isFinalFailure ? OfflineOrderStatuses.Failed : OfflineOrderStatuses.Pending;
            order.ErrorMessage = TruncateError(ex.Message);
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);

            return new ReplayOfflineOrderResult
            {
                OrderId = order.Id,
                Success = false,
                ErrorMessage = order.ErrorMessage
            };
        }
    }

    private static bool IsReceiptNumberConflict(PaymentResult result) =>
        string.Equals(result.DiagnosticCode, "RECEIPT_NUMBER_CONFLICT", StringComparison.OrdinalIgnoreCase)
        || result.Errors.Any(e => e.Contains("receipt number", StringComparison.OrdinalIgnoreCase)
                                  || e.Contains("BelegNr", StringComparison.OrdinalIgnoreCase));

    private Guid RequireTenantId()
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant context is required for offline order operations.");

        return tenantId;
    }

    private async Task EnsureCashRegisterExistsAsync(Guid cashRegisterId, Guid tenantId, CancellationToken ct)
    {
        var exists = await _context.CashRegisters
            .AsNoTracking()
            .AnyAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId && r.IsActive, ct)
            .ConfigureAwait(false);

        if (!exists)
            throw new KeyNotFoundException($"Cash register '{cashRegisterId}' was not found.");
    }

    private static void ValidatePaymentMethodAllowed(string paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new ArgumentException("Payment method is required.", nameof(paymentMethod));

        if (string.Equals(paymentMethod.Trim(), "voucher", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Voucher payments cannot be queued as offline orders.");
    }

    private static string GenerateOfflineOrderId(DateTime utcNow)
    {
        var random = Random.Shared.Next(0, 10000);
        return $"OFFLINE-{utcNow:yyyyMMddHHmmss}-{random:D4}";
    }

    private static string SerializeOrderData(object orderData) =>
        orderData switch
        {
            JsonElement jsonElement => jsonElement.GetRawText(),
            string json => json,
            _ => JsonSerializer.Serialize(orderData)
        };

    private static CreatePaymentRequest? TryParsePaymentRequest(string orderDataJson)
    {
        using var doc = JsonDocument.Parse(orderDataJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("customerId", out _) || root.TryGetProperty("CustomerId", out _))
            return JsonSerializer.Deserialize<CreatePaymentRequest>(orderDataJson);

        if (root.TryGetProperty("paymentRequest", out var nested)
            || root.TryGetProperty("PaymentRequest", out nested))
            return JsonSerializer.Deserialize<CreatePaymentRequest>(nested.GetRawText());

        return JsonSerializer.Deserialize<CreatePaymentRequest>(orderDataJson);
    }

    private string ResolveActorUserId() =>
        _httpContextAccessor.HttpContext?.User.GetActorUserId()
        ?? throw new InvalidOperationException("Authenticated user is required for offline order replay.");

    private string ResolveActorUserIdOrSystem() =>
        _httpContextAccessor.HttpContext?.User.GetActorUserId() ?? "system";

    private string ResolveActorRoleOrSystem() =>
        _httpContextAccessor.HttpContext?.User.GetActorRole() ?? "System";

    private static OfflineOrderResponse ToResponse(OfflineOrder order)
    {
        var remaining = order.ExpiresAtUtc - DateTime.UtcNow;
        var hoursRemaining = remaining.TotalHours <= 0
            ? 0
            : (int)Math.Ceiling(remaining.TotalHours);

        return new OfflineOrderResponse
        {
            Id = order.Id,
            OfflineOrderId = order.OfflineOrderId,
            Status = order.Status,
            ExpiresAtUtc = order.ExpiresAtUtc,
            HoursRemaining = hoursRemaining
        };
    }

    private static string TruncateError(string? message) =>
        string.IsNullOrWhiteSpace(message)
            ? "Offline order replay failed."
            : message.Length <= 2000 ? message : message[..2000];
}
