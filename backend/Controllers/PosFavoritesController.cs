using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

[ApiController]
[Route("api/pos/favorites")]
[Authorize(Roles = $"{Roles.Cashier},{Roles.Manager}")]
public class PosFavoritesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosFavoritesController(AppDbContext context, ICurrentTenantAccessor tenantAccessor)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<CashierFavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CashierFavoriteDto>>> GetFavorites(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var favorites = await _context.CashierFavorites
            .AsNoTracking()
            .Include(f => f.Product)
            .Where(f => f.CashierId == userId && f.TenantId == tenantId && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .Select(f => new CashierFavoriteDto
            {
                Id = f.Id,
                ProductId = f.ProductId,
                ProductName = f.Product.Name,
                ProductPrice = f.Product.Price,
                SortOrder = f.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return Ok(favorites);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AddFavorite(
        [FromBody] AddFavoriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var productExists = await _context.Products.AsNoTracking()
            .AnyAsync(p => p.Id == request.ProductId && p.TenantId == tenantId && p.IsActive, cancellationToken);
        if (!productExists)
            return NotFound(new { message = "Product not found" });

        var existing = await _context.CashierFavorites
            .FirstOrDefaultAsync(
                f => f.CashierId == userId && f.TenantId == tenantId && f.ProductId == request.ProductId,
                cancellationToken);
        if (existing is { IsActive: true })
            return Ok();

        var maxOrder = await _context.CashierFavorites
            .Where(f => f.CashierId == userId && f.TenantId == tenantId && f.IsActive)
            .MaxAsync(f => (int?)f.SortOrder, cancellationToken) ?? 0;

        if (existing != null)
        {
            existing.IsActive = true;
            existing.SortOrder = maxOrder + 1;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.CashierFavorites.Add(new CashierFavorite
            {
                TenantId = tenantId.Value,
                CashierId = userId,
                ProductId = request.ProductId,
                SortOrder = maxOrder + 1,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var favorite = await _context.CashierFavorites
            .FirstOrDefaultAsync(
                f => f.Id == id && f.CashierId == userId && f.TenantId == tenantId,
                cancellationToken);

        if (favorite == null)
            return NotFound();

        _context.CashierFavorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderFavorites(
        [FromBody] ReorderFavoritesRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null)
            return NotFound();

        var favorites = await _context.CashierFavorites
            .Where(f => f.CashierId == userId && f.TenantId == tenantId && f.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var fav in favorites)
        {
            var newOrder = request.OrderIds.IndexOf(fav.Id);
            if (newOrder >= 0)
            {
                fav.SortOrder = newOrder;
                fav.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
