using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Preorder;

public sealed class PreorderService : IPreorderService
{
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<PreorderService> _logger;

    public PreorderService(
        AppDbContext context,
        ISettingsTenantResolver tenantResolver,
        ILogger<PreorderService> logger)
    {
        _context = context;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task TryCreateFromSuccessfulPaymentAsync(
        PaymentDetails payment,
        CreatePaymentRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsPreorder || request.IsStorno || request.IsRefund)
            return;

        if (!string.IsNullOrWhiteSpace(payment.RksvSpecialReceiptKind))
            return;

        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return;

        var existing = await ScopedPreorders(tenantId.Value)
            .FirstOrDefaultAsync(o => o.SourcePaymentId == payment.Id, cancellationToken);
        if (existing != null)
            return;

        var notes = string.IsNullOrWhiteSpace(request.PreorderCustomerNotes)
            ? request.Notes
            : request.PreorderCustomerNotes;

        var settings = await LoadSettingsRowAsync(tenantId.Value, cancellationToken);
        var weeks = PreorderPolicyDefaults.ClampWeeks(settings?.PreorderPickupDeadlineWeeks ?? 0);
        var remaining = decimal.Round(Math.Max(0, request.PreorderRemainingAmount), 2, MidpointRounding.AwayFromZero);
        var paid = decimal.Round(payment.TotalAmount, 2, MidpointRounding.AwayFromZero);
        var now = DateTime.UtcNow;
        var viennaDate = PreorderNumberFormatter.ViennaDate(now);

        var order = new Models.Order
        {
            OrderId = BuildOrderId(payment.ReceiptNumber),
            TableNumber = payment.TableNumber,
            CustomerName = payment.CustomerName,
            CustomerId = payment.CustomerId == Guid.Empty ? null : payment.CustomerId,
            Notes = notes,
            OrderDate = now,
            Status = OrderStatus.Pending,
            Subtotal = payment.TotalAmount - payment.TaxAmount,
            TaxAmount = payment.TaxAmount,
            DiscountAmount = 0,
            TotalAmount = paid + remaining,
            CreatedBy = actorUserId,
            TenantId = tenantId.Value,
            IsPreorder = true,
            PreorderStatus = PreorderStatuses.Pending,
            PreorderCustomerNotes = notes,
            SourcePaymentId = payment.Id,
            LastPreorderPaymentId = payment.Id,
            ReceiptNumber = payment.ReceiptNumber,
            PreorderNumber = await AllocateNumberAsync(tenantId.Value, viennaDate, cancellationToken),
            PreorderPaidAmount = paid,
            PreorderRemainingAmount = remaining,
            PreorderPickupWeeks = weeks,
            PreorderPickupDeadline = DateTime.SpecifyKind(viennaDate.AddDays(weeks * 7), DateTimeKind.Utc)
        };

        foreach (var item in request.Items ?? [])
        {
            if (item.ProductId == Guid.Empty || item.Quantity <= 0)
                continue;

            var productName = await _context.Products.AsNoTracking()
                .Where(p => p.Id == item.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);

            order.Items.Add(new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = item.ProductId,
                ProductName = string.IsNullOrWhiteSpace(productName) ? "Artikel" : productName,
                Quantity = item.Quantity,
                UnitPrice = 0,
                TaxRate = 0,
                TaxAmount = 0,
                DiscountAmount = 0,
                TotalAmount = 0,
                CreatedBy = actorUserId
            });
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Preorder created OrderId={OrderId} Number={Number} PaymentId={PaymentId} Receipt={Receipt} Remaining={Remaining}",
            order.Id,
            order.PreorderNumber,
            payment.Id,
            payment.ReceiptNumber,
            remaining);
    }

    public async Task MarkCancelledForPaymentAsync(
        Guid sourcePaymentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return;

        var order = await ScopedPreorders(tenantId.Value)
            .FirstOrDefaultAsync(
                o => o.SourcePaymentId == sourcePaymentId || o.LastPreorderPaymentId == sourcePaymentId,
                cancellationToken);
        if (order is null || order.PreorderStatus == PreorderStatuses.Cancelled)
            return;

        order.PreorderStatus = PreorderStatuses.Cancelled;
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PreorderBalanceGuardResult> ValidateBalancePaymentAsync(
        Guid orderId,
        decimal paymentAmount,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return PreorderBalanceGuardResult.Fail("PREORDER_TENANT_REQUIRED", "Tenant context is required.");

        var order = await ScopedPreorders(tenantId.Value)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return PreorderBalanceGuardResult.Fail("PREORDER_NOT_FOUND", "Pre-order was not found.");

        if (order.PreorderStatus is PreorderStatuses.Cancelled or PreorderStatuses.Collected)
            return PreorderBalanceGuardResult.Fail("PREORDER_BALANCE_CLOSED", "Pre-order cannot accept a further payment.");

        var remaining = decimal.Round(order.PreorderRemainingAmount, 2, MidpointRounding.AwayFromZero);
        if (remaining <= PreorderPolicyDefaults.MoneyTolerance)
            return PreorderBalanceGuardResult.Fail("PREORDER_BALANCE_ZERO", "No remaining pre-order amount.");

        var paidNow = decimal.Round(paymentAmount, 2, MidpointRounding.AwayFromZero);
        if (paidNow < 0.01m)
            return PreorderBalanceGuardResult.Fail("PREORDER_BALANCE_AMOUNT", "Balance payment must be greater than zero.");

        if (paidNow - remaining > PreorderPolicyDefaults.MoneyTolerance)
            return PreorderBalanceGuardResult.Fail("PREORDER_BALANCE_EXCEEDS", "Payment exceeds the remaining pre-order amount.");

        return PreorderBalanceGuardResult.Success();
    }

    public async Task ApplyBalancePaymentAsync(
        Guid orderId,
        PaymentDetails payment,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return;

        var order = await ScopedPreorders(tenantId.Value)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return;

        var paidNow = decimal.Round(payment.TotalAmount, 2, MidpointRounding.AwayFromZero);
        var remaining = decimal.Round(order.PreorderRemainingAmount, 2, MidpointRounding.AwayFromZero);
        var applied = Math.Min(paidNow, remaining);
        order.PreorderPaidAmount = decimal.Round(order.PreorderPaidAmount + applied, 2, MidpointRounding.AwayFromZero);
        order.PreorderRemainingAmount = decimal.Round(remaining - applied, 2, MidpointRounding.AwayFromZero);
        order.LastPreorderPaymentId = payment.Id;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PreorderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return null;

        var order = await ScopedPreorders(tenantId.Value)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? null : Map(order);
    }

    public async Task<PreorderDto?> GetByReceiptNumberAsync(
        string receiptNumber,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return null;

        var key = receiptNumber.Trim();
        if (key.Length == 0)
            return null;

        var order = await ScopedPreorders(tenantId.Value)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.ReceiptNumber == key || o.PreorderNumber == key,
                cancellationToken);
        return order is null ? null : Map(order);
    }

    public async Task<PreorderListResponseDto> ListAsync(
        string? status,
        string? receiptNumber,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return new PreorderListResponseDto();

        var scoped = ScopedPreorders(tenantId.Value).AsNoTracking();
        var pending = await scoped.CountAsync(o => o.PreorderStatus == PreorderStatuses.Pending, cancellationToken);
        var ready = await scoped.CountAsync(o => o.PreorderStatus == PreorderStatuses.Ready, cancellationToken);
        var collected = await scoped.CountAsync(o => o.PreorderStatus == PreorderStatuses.Collected, cancellationToken);
        var cancelled = await scoped.CountAsync(o => o.PreorderStatus == PreorderStatuses.Cancelled, cancellationToken);

        var query = scoped;
        if (!string.IsNullOrWhiteSpace(status) && PreorderStatuses.All.Contains(status.Trim()))
        {
            var normalized = PreorderStatuses.Normalize(status);
            query = query.Where(o => o.PreorderStatus == normalized);
        }

        if (!string.IsNullOrWhiteSpace(receiptNumber))
        {
            var key = receiptNumber.Trim();
            query = query.Where(o => o.ReceiptNumber == key || o.PreorderNumber == key);
        }

        var rows = await query
            .OrderByDescending(o => o.OrderDate)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PreorderListResponseDto
        {
            Pending = pending,
            Ready = ready,
            Collected = collected,
            Cancelled = cancelled,
            Orders = rows.Select(Map).ToList()
        };
    }

    public async Task<PreorderStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var list = await ListAsync(status: null, receiptNumber: null, take: 1, cancellationToken);
        return new PreorderStatsDto
        {
            Pending = list.Pending,
            Ready = list.Ready,
            Collected = list.Collected,
            Cancelled = list.Cancelled
        };
    }

    public async Task<PreorderDto?> SetOperationalStatusAsync(
        Guid id,
        string status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return null;

        var order = await ScopedPreorders(tenantId.Value)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
            return null;

        var next = PreorderStatuses.Normalize(status);
        if (next == PreorderStatuses.Cancelled)
            throw new InvalidOperationException("PREORDER_CANCEL_REQUIRES_STORNO");

        if (next == PreorderStatuses.Collected
            && order.PreorderRemainingAmount > PreorderPolicyDefaults.MoneyTolerance)
        {
            throw new InvalidOperationException("PREORDER_BALANCE_OPEN");
        }

        if (!PreorderStatusTransitions.CanTransition(order.PreorderStatus, next, isFiscalCancel: false))
            throw new InvalidOperationException("PREORDER_STATUS_TRANSITION_DENIED");

        var now = DateTime.UtcNow;
        order.PreorderStatus = next;
        order.Status = PreorderStatusTransitions.ToKitchenStatus(next);
        order.UpdatedAt = now;
        if (next == PreorderStatuses.Ready)
            order.PreorderReadyAt = now;
        if (next == PreorderStatuses.Collected)
            order.PreorderCollectedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        return Map(order);
    }

    public async Task<PreorderSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            return DefaultSettings();

        var row = await LoadSettingsRowAsync(tenantId.Value, cancellationToken);
        return MapSettings(row);
    }

    public async Task<PreorderSettingsDto> UpdateSettingsAsync(
        PreorderSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await RequireTenantIdAsync(cancellationToken);
        if (tenantId is null)
            throw new InvalidOperationException("PREORDER_TENANT_REQUIRED");

        var row = await _context.CompanySettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, cancellationToken);
        if (row is null)
            throw new InvalidOperationException("PREORDER_SETTINGS_NOT_FOUND");

        row.PreorderPickupDeadlineWeeks = PreorderPolicyDefaults.ClampWeeks(request.PickupDeadlineWeeks);
        row.PreorderCancellationPolicyText = PreorderPolicyDefaults.NormalizePolicy(request.CancellationPolicyText);
        row.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return MapSettings(row);
    }

    private async Task<string> AllocateNumberAsync(Guid tenantId, DateTime viennaDate, CancellationToken cancellationToken)
    {
        var prefix = PreorderNumberFormatter.BuildPrefix(viennaDate);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var existing = await _context.Orders.AsNoTracking()
                .Where(o => o.IsPreorder && o.TenantId == tenantId && o.PreorderNumber != null && o.PreorderNumber.StartsWith(prefix))
                .Select(o => o.PreorderNumber!)
                .ToListAsync(cancellationToken);

            var nextSeq = 1;
            foreach (var number in existing)
            {
                if (number.Length <= prefix.Length)
                    continue;
                if (int.TryParse(number[prefix.Length..], out var seq) && seq >= nextSeq)
                    nextSeq = seq + 1;
            }

            var candidate = PreorderNumberFormatter.Format(viennaDate, nextSeq);
            var clash = await _context.Orders.AsNoTracking()
                .AnyAsync(o => o.TenantId == tenantId && o.PreorderNumber == candidate, cancellationToken);
            if (!clash)
                return candidate;
        }

        return PreorderNumberFormatter.Format(viennaDate, Random.Shared.Next(1000, 9999));
    }

    private IQueryable<Models.Order> ScopedPreorders(Guid tenantId) =>
        _context.Orders.Where(o => o.IsPreorder && o.TenantId == tenantId);

    private async Task<Guid?> RequireTenantIdAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        return tenantId == Guid.Empty ? null : tenantId;
    }

    private Task<CompanySettings?> LoadSettingsRowAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

    private static PreorderSettingsDto DefaultSettings() => new()
    {
        PickupDeadlineWeeks = PreorderPolicyDefaults.PickupDeadlineWeeks,
        CancellationPolicyText = PreorderPolicyDefaults.CancellationPolicyText
    };

    private static PreorderSettingsDto MapSettings(CompanySettings? row) => new()
    {
        PickupDeadlineWeeks = PreorderPolicyDefaults.ClampWeeks(row?.PreorderPickupDeadlineWeeks ?? 0),
        CancellationPolicyText = PreorderPolicyDefaults.NormalizePolicy(row?.PreorderCancellationPolicyText)
    };

    private static PreorderDto Map(Models.Order order) => new()
    {
        Id = order.Id,
        OrderId = order.OrderId,
        ReceiptNumber = order.ReceiptNumber,
        PreorderNumber = order.PreorderNumber,
        SourcePaymentId = order.SourcePaymentId,
        Status = PreorderStatuses.Normalize(order.PreorderStatus),
        CustomerName = order.CustomerName,
        CustomerNotes = order.PreorderCustomerNotes,
        TotalAmount = order.TotalAmount,
        PaidAmount = order.PreorderPaidAmount,
        RemainingAmount = order.PreorderRemainingAmount,
        OrderDate = order.OrderDate,
        PickupDeadlineUtc = order.PreorderPickupDeadline,
        PickupWeeks = order.PreorderPickupWeeks,
        ReadyAtUtc = order.PreorderReadyAt,
        CollectedAtUtc = order.PreorderCollectedAt
    };

    private static string BuildOrderId(string? receiptNumber)
    {
        var suffix = string.IsNullOrWhiteSpace(receiptNumber)
            ? Guid.NewGuid().ToString("N")[..8]
            : receiptNumber.Trim();
        var id = $"PRE-{suffix}";
        return id.Length <= 50 ? id : id[..50];
    }
}
