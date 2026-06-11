using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PosSplitSessionService : IPosSplitSessionService
{
    private static readonly int[] PosTableNumbers = Enumerable.Range(1, 10).ToArray();

    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IPosCartTableOpsService _cartTableOps;
    private readonly ILogger<PosSplitSessionService> _logger;

    public PosSplitSessionService(
        AppDbContext context,
        ISettingsTenantResolver tenantResolver,
        IPosCartTableOpsService cartTableOps,
        ILogger<PosSplitSessionService> logger)
    {
        _context = context;
        _tenantResolver = tenantResolver;
        _cartTableOps = cartTableOps;
        _logger = logger;
    }

    public async Task<SplitSessionDto> StartSplitAsync(
        string cashierUserId,
        StartSplitRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(
                c => c.Id == request.CartId && c.UserId == cashierUserId && c.Status == CartStatus.Active,
                cancellationToken);

        if (cart == null || cart.Items.Count == 0)
            throw new PosSplitSessionException("Cart is empty or not found", 404);

        var activeSession = await _context.SplitSessions.AnyAsync(
            s => s.OriginalCartId == cart.Id && s.TenantId == tenantId && !s.IsCompleted && s.IsActive,
            cancellationToken);
        if (activeSession)
            throw new PosSplitSessionException("An active split session already exists for this cart", 409);

        var splitLines = cart.Items.Select(item => new SplitItem
        {
            ProductId = item.ProductId,
            SourceCartItemId = item.Id,
            Quantity = item.Quantity,
            Price = item.UnitPrice,
            CustomerName = string.Empty,
            SeatNumber = 0,
        }).ToList();

        var session = new SplitSession
        {
            TenantId = tenantId,
            OriginalCartId = cart.Id,
            CashierId = cashierUserId,
            IsCompleted = false,
            SplitItems = splitLines,
        };

        _context.SplitSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Split session {SessionId} started for cart {CartKey} with {LineCount} lines",
            session.Id,
            cart.CartId,
            splitLines.Count);

        return await MapSessionDtoAsync(session.Id, cancellationToken)
               ?? throw new PosSplitSessionException("Failed to load split session", 500);
    }

    public async Task AssignItemAsync(
        string cashierUserId,
        Guid sessionId,
        AssignItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var session = await _context.SplitSessions
            .Include(s => s.SplitItems)
            .FirstOrDefaultAsync(
                s => s.Id == sessionId && s.TenantId == tenantId && s.CashierId == cashierUserId && s.IsActive,
                cancellationToken);

        if (session == null)
            throw new PosSplitSessionException("Split session not found", 404);

        if (session.IsCompleted)
            throw new PosSplitSessionException("Split session is already completed", 409);

        var item = session.SplitItems.FirstOrDefault(i => i.Id == request.ItemId);
        if (item == null)
            throw new PosSplitSessionException("Split item not found", 404);

        item.CustomerName = (request.CustomerName ?? string.Empty).Trim();
        item.SeatNumber = request.SeatNumber;
        item.UpdatedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> CompleteSplitAsync(
        string cashierUserId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var session = await _context.SplitSessions
            .Include(s => s.SplitItems)
            .Include(s => s.OriginalCart)
            .FirstOrDefaultAsync(
                s => s.Id == sessionId && s.TenantId == tenantId && s.CashierId == cashierUserId && s.IsActive,
                cancellationToken);

        if (session == null)
            throw new PosSplitSessionException("Split session not found", 404);

        if (session.IsCompleted)
            throw new PosSplitSessionException("Split session is already completed", 409);

        var unassigned = session.SplitItems.Where(i => string.IsNullOrWhiteSpace(i.CustomerName)).ToList();
        if (unassigned.Count > 0)
            throw new PosSplitSessionException("All split items must be assigned to a customer before completing", 400);

        var groups = session.SplitItems
            .GroupBy(i => i.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var occupiedTables = await _context.Carts
            .Where(c => c.UserId == cashierUserId && c.Status == CartStatus.Active && c.Items.Any())
            .Select(c => c.TableNumber)
            .ToListAsync(cancellationToken);

        var reservedTables = new HashSet<int>(
            occupiedTables.Where(t => t.HasValue).Select(t => t!.Value));

        var newCartIds = new List<Guid>();
        var originalCart = session.OriginalCart
            ?? throw new PosSplitSessionException("Original cart not found", 404);

        foreach (var group in groups)
        {
            var tableNumber = ReserveNextTable(reservedTables, originalCart.TableNumber);
            if (tableNumber == null)
                throw new PosSplitSessionException("No free table available for split carts", 409);

            reservedTables.Add(tableNumber.Value);

            var newCart = new Cart
            {
                CartId = Guid.NewGuid().ToString(),
                TableNumber = tableNumber,
                UserId = cashierUserId,
                WaiterName = originalCart.WaiterName ?? "Kasiyer",
                CustomerId = originalCart.CustomerId,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Status = CartStatus.Active,
            };

            foreach (var splitItem in group)
            {
                newCart.Items.Add(new CartItem
                {
                    CartId = newCart.CartId,
                    ProductId = splitItem.ProductId,
                    Quantity = splitItem.Quantity,
                    UnitPrice = splitItem.Price,
                });
            }

            _context.Carts.Add(newCart);
            newCartIds.Add(newCart.Id);
        }

        var originalCartItems = await _context.CartItems
            .Where(i => i.CartId == originalCart.CartId)
            .ToListAsync(cancellationToken);
        _context.CartItems.RemoveRange(originalCartItems);

        session.IsCompleted = true;
        session.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Split session {SessionId} completed; created {CartCount} carts",
            sessionId,
            newCartIds.Count);

        return newCartIds;
    }

    public async Task<SplitSessionDto?> GetSessionAsync(
        string cashierUserId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var exists = await _context.SplitSessions.AsNoTracking()
            .AnyAsync(
                s => s.Id == sessionId && s.TenantId == tenantId && s.CashierId == cashierUserId && s.IsActive,
                cancellationToken);
        if (!exists)
            return null;

        return await MapSessionDtoAsync(sessionId, cancellationToken);
    }

    public async Task<SplitSessionDto> MergeSessionsToTableAsync(
        string cashierUserId,
        MergeSplitSessionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        var sessions = await _context.SplitSessions
            .Include(s => s.SplitItems)
            .Include(s => s.OriginalCart)
            .Where(s => request.SessionIds.Contains(s.Id)
                        && s.TenantId == tenantId
                        && s.CashierId == cashierUserId
                        && s.IsActive
                        && !s.IsCompleted)
            .ToListAsync(cancellationToken);

        if (sessions.Count != request.SessionIds.Count)
            throw new PosSplitSessionException("One or more split sessions were not found", 404);

        var tableNumbers = sessions
            .Select(s => s.OriginalCart?.TableNumber)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .Distinct()
            .ToList();

        if (tableNumbers.Count == 0)
            throw new PosSplitSessionException("Split sessions have no associated table", 400);

        var sourceTable = tableNumbers[0];
        foreach (var other in tableNumbers.Skip(1))
        {
            await _cartTableOps.MergeTablesAsync(
                cashierUserId,
                new MergeTableCartsRequest { SourceTableNumber = other, TargetTableNumber = sourceTable },
                cancellationToken);
        }

        if (sourceTable != request.TargetTableNumber)
        {
            var itemIds = sessions
                .SelectMany(s => s.SplitItems)
                .Where(i => i.SourceCartItemId.HasValue)
                .Select(i => i.SourceCartItemId!.Value)
                .Distinct()
                .ToList();

            if (itemIds.Count > 0)
            {
                await _cartTableOps.SplitItemsAsync(
                    cashierUserId,
                    new SplitCartItemsRequest
                    {
                        SourceTableNumber = sourceTable,
                        TargetTableNumber = request.TargetTableNumber,
                        ItemIds = itemIds,
                    },
                    cancellationToken);
            }
        }

        foreach (var splitSession in sessions)
        {
            splitSession.IsCompleted = true;
            splitSession.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var primary = sessions.OrderBy(s => s.CreatedAt).First();
        return await MapSessionDtoAsync(primary.Id, cancellationToken)
               ?? throw new PosSplitSessionException("Failed to load merged split session", 500);
    }

    private static int? ReserveNextTable(HashSet<int> reserved, int? preferNear)
    {
        if (preferNear.HasValue && !reserved.Contains(preferNear.Value))
            return preferNear;

        return PosTableNumbers.FirstOrDefault(t => !reserved.Contains(t)) is var found && found > 0
            ? found
            : null;
    }

    private async Task<SplitSessionDto?> MapSessionDtoAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _context.SplitSessions.AsNoTracking()
            .Include(s => s.SplitItems)
            .Include(s => s.OriginalCart)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
            return null;

        var productIds = session.SplitItems.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var items = session.SplitItems
            .OrderBy(i => i.SeatNumber)
            .ThenBy(i => i.CreatedAt)
            .Select(i =>
            {
                products.TryGetValue(i.ProductId, out var product);
                return new SplitItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = product?.Name ?? "Unknown",
                    SourceCartItemId = i.SourceCartItemId,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    LineTotal = i.Price * i.Quantity,
                    CustomerName = i.CustomerName,
                    SeatNumber = i.SeatNumber,
                };
            })
            .ToList();

        return new SplitSessionDto
        {
            Id = session.Id,
            OriginalCartId = session.OriginalCartId,
            OriginalCartKey = session.OriginalCart?.CartId ?? string.Empty,
            TableNumber = session.OriginalCart?.TableNumber,
            IsCompleted = session.IsCompleted,
            CreatedAt = session.CreatedAt,
            Items = items,
            GrandTotal = items.Sum(i => i.LineTotal),
        };
    }
}

public sealed class PosSplitSessionException : Exception
{
    public int StatusCode { get; }

    public PosSplitSessionException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
