using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Data.Repositories;

/// <summary>
/// Product repository scoped to <see cref="ISettingsTenantResolver"/> effective tenant (no global query filters).
/// </summary>
public class TenantScopedProductRepository : GenericRepository<Product>
{
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public TenantScopedProductRepository(
        AppDbContext context,
        ILogger<GenericRepository<Product>> logger,
        ISettingsTenantResolver settingsTenantResolver)
        : base(context, logger)
    {
        _settingsTenantResolver = settingsTenantResolver;
    }

    private async Task<Guid> ResolveTenantIdAsync(CancellationToken cancellationToken = default) =>
        await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

    public override async Task<IEnumerable<Product>> GetAllAsync()
    {
        var tid = await ResolveTenantIdAsync();
        return await _dbSet
            .Where(e => e.IsActive && e.TenantId == tid)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public override async Task<IEnumerable<Product>> GetAsync(Expression<Func<Product, bool>> predicate)
    {
        var tid = await ResolveTenantIdAsync();
        return await _dbSet
            .Where(e => e.IsActive && e.TenantId == tid)
            .Where(predicate)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public override async Task<(IEnumerable<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<Product, bool>>? predicate = null,
        Expression<Func<Product, object>>? orderBy = null,
        bool ascending = true)
    {
        var tid = await ResolveTenantIdAsync();
        var query = _dbSet.Where(e => e.IsActive && e.TenantId == tid);
        if (predicate != null)
            query = query.Where(predicate);
        var totalCount = await query.CountAsync();
        if (orderBy != null)
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
        else
            query = query.OrderByDescending(e => e.CreatedAt);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }

    public override async Task<Product?> GetByIdAsync(Guid id)
    {
        var tid = await ResolveTenantIdAsync();
        return await _dbSet.FirstOrDefaultAsync(e => e.Id == id && e.IsActive && e.TenantId == tid);
    }

    public override async Task<bool> ExistsAsync(Guid id)
    {
        var tid = await ResolveTenantIdAsync();
        return await _dbSet.AnyAsync(e => e.Id == id && e.IsActive && e.TenantId == tid);
    }

    public override async Task<int> CountAsync(Expression<Func<Product, bool>>? predicate = null)
    {
        var tid = await ResolveTenantIdAsync();
        var query = _dbSet.Where(e => e.IsActive && e.TenantId == tid);
        if (predicate != null)
            query = query.Where(predicate);
        return await query.CountAsync();
    }

    public override async Task<Product> AddAsync(Product entity)
    {
        entity.TenantId = await ResolveTenantIdAsync();
        return await base.AddAsync(entity);
    }

    public override async Task<Product> UpdateAsync(Product entity)
    {
        var existingEntity = await GetByIdAsync(entity.Id);
        if (existingEntity == null)
        {
            throw new InvalidOperationException($"Entity of type {typeof(Product).Name} with ID {entity.Id} not found");
        }

        entity.TenantId = existingEntity.TenantId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.CreatedAt = existingEntity.CreatedAt;
        _context.Entry(existingEntity).State = EntityState.Detached;
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Entity of type {EntityType} updated with ID {Id}", typeof(Product).Name, entity.Id);
        return entity;
    }

    public override async Task<bool> HardDeleteAsync(Guid id)
    {
        var tid = await ResolveTenantIdAsync();
        var entity = await _dbSet.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tid);
        if (entity == null)
            return false;
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Entity of type {EntityType} hard deleted with ID {Id}", typeof(Product).Name, id);
        return true;
    }
}
