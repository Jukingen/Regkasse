using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using PosCartResponse = KasseAPI_Final.Controllers.CartResponse;

namespace KasseAPI_Final.Services;

public sealed class PosCartTableOpsService : IPosCartTableOpsService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PosCartTableOpsService> _logger;

    public PosCartTableOpsService(AppDbContext context, ILogger<PosCartTableOpsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PosCartResponse> SplitItemsAsync(
        string userId,
        SplitCartItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceTableNumber == request.TargetTableNumber)
            throw new PosCartTableOpsException("Source and target table must differ", 400);

        var sourceCart = await LoadUserCartAsync(userId, request.SourceTableNumber, cancellationToken);
        if (sourceCart == null || sourceCart.Items.Count == 0)
            throw new PosCartTableOpsException("Source cart is empty", 400);

        var targetCart = await GetOrCreateCartAsync(userId, request.TargetTableNumber, cancellationToken);

        var itemIdSet = request.ItemIds.ToHashSet();
        var itemsToMove = sourceCart.Items.Where(i => itemIdSet.Contains(i.Id)).ToList();
        if (itemsToMove.Count == 0)
            throw new PosCartTableOpsException("No matching cart items found on source table", 404);

        foreach (var item in itemsToMove)
        {
            item.CartId = targetCart.CartId;
            item.UpdatedAt = DateTime.UtcNow;
        }

        sourceCart.UpdatedAt = DateTime.UtcNow;
        targetCart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Split {Count} cart items from table {Source} to {Target} for user {UserId}",
            itemsToMove.Count,
            request.SourceTableNumber,
            request.TargetTableNumber,
            userId);

        return await BuildCartResponseAsync(targetCart.CartId, cancellationToken);
    }

    public async Task<PosCartResponse> MergeTablesAsync(
        string userId,
        MergeTableCartsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceTableNumber == request.TargetTableNumber)
            throw new PosCartTableOpsException("Source and target table must differ", 400);

        var sourceCart = await LoadUserCartAsync(userId, request.SourceTableNumber, cancellationToken);
        if (sourceCart == null || sourceCart.Items.Count == 0)
            throw new PosCartTableOpsException("Source cart is empty", 400);

        var targetCart = await GetOrCreateCartAsync(userId, request.TargetTableNumber, cancellationToken);

        foreach (var item in sourceCart.Items.ToList())
        {
            item.CartId = targetCart.CartId;
            item.UpdatedAt = DateTime.UtcNow;
        }

        _context.Carts.Remove(sourceCart);
        targetCart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Merged table {Source} into {Target} for user {UserId}",
            request.SourceTableNumber,
            request.TargetTableNumber,
            userId);

        return await BuildCartResponseAsync(targetCart.CartId, cancellationToken);
    }

    private async Task<Cart?> LoadUserCartAsync(string userId, int tableNumber, CancellationToken cancellationToken) =>
        await _context.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Modifiers)
            .FirstOrDefaultAsync(
                c => c.UserId == userId && c.TableNumber == tableNumber && c.Status == CartStatus.Active,
                cancellationToken);

    private async Task<Cart> GetOrCreateCartAsync(string userId, int tableNumber, CancellationToken cancellationToken)
    {
        var existing = await LoadUserCartAsync(userId, tableNumber, cancellationToken);
        if (existing != null)
            return existing;

        var cart = new Cart
        {
            CartId = Guid.NewGuid().ToString(),
            TableNumber = tableNumber,
            UserId = userId,
            WaiterName = "Kasiyer",
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Status = CartStatus.Active,
        };
        _context.Carts.Add(cart);
        await _context.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private async Task<PosCartResponse> BuildCartResponseAsync(string cartId, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Modifiers)
            .FirstAsync(c => c.CartId == cartId, cancellationToken);

        var productIds = cart.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return CartResponseBuilder.Build(cart, products);
    }
}

public sealed class PosCartTableOpsException : Exception
{
    public int StatusCode { get; }

    public PosCartTableOpsException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
