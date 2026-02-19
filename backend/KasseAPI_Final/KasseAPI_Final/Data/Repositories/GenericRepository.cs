using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Models;
using System.Linq.Expressions;

namespace KasseAPI_Final.Data.Repositories
{
    /// <summary>
    /// Generic repository pattern implementation
    /// </summary>
    /// <typeparam name="T">Entity tipi</typeparam>
    public class GenericRepository<T> : IGenericRepository<T> where T : class, IEntity
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILogger<GenericRepository<T>> _logger;

        public GenericRepository(AppDbContext context, ILogger<GenericRepository<T>> logger)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _logger = logger;
        }

        /// <summary>
        /// Tüm entity'leri getir
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                return await _dbSet
                    .Where(e => e.IsActive)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Filtrelenmiş entity'leri getir
        /// </summary>
        public virtual async Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                return await _dbSet
                    .Where(e => e.IsActive)
                    .Where(predicate)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Sayfalama ile entity'leri getir
        /// </summary>
        public virtual async Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<T, bool>>? predicate = null,
            Expression<Func<T, object>>? orderBy = null,
            bool ascending = true)
        {
            try
            {
                var query = _dbSet.Where(e => e.IsActive);

                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                var totalCount = await query.CountAsync();

                if (orderBy != null)
                {
                    query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
                }
                else
                {
                    query = query.OrderByDescending(e => e.CreatedAt);
                }

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return (items, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// ID'ye göre entity getir
        /// </summary>
        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            try
            {
                return await _dbSet
                    .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <summary>
        /// Entity ekle
        /// </summary>
        public virtual async Task<T> AddAsync(T entity)
        {
            try
            {
                entity.CreatedAt = DateTime.UtcNow;
                entity.IsActive = true;

                await _dbSet.AddAsync(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Entity of type {EntityType} added with ID {Id}", typeof(T).Name, entity.Id);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity of type {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Entity güncelle
        /// </summary>
        public virtual async Task<T> UpdateAsync(T entity)
        {
            try
            {
                var existingEntity = await GetByIdAsync(entity.Id);
                if (existingEntity == null)
                {
                    throw new InvalidOperationException($"Entity of type {typeof(T).Name} with ID {entity.Id} not found");
                }

                entity.UpdatedAt = DateTime.UtcNow;
                entity.CreatedAt = existingEntity.CreatedAt; // Orijinal oluşturma tarihini koru

                // EF Core tracking hatasını önlemek için mevcut entity'i detach et
                // "The instance of entity type '...' cannot be tracked..."
                _context.Entry(existingEntity).State = EntityState.Detached;

                _dbSet.Update(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Entity of type {EntityType} updated with ID {Id}", typeof(T).Name, entity.Id);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating entity of type {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                throw;
            }
        }

        /// <summary>
        /// Entity sil (soft delete)
        /// </summary>
        public virtual async Task<bool> DeleteAsync(Guid id)
        {
            try
            {
                var entity = await GetByIdAsync(id);
                if (entity == null)
                {
                    return false;
                }

                entity.IsActive = false;
                entity.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Entity of type {EntityType} soft deleted with ID {Id}", typeof(T).Name, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <summary>
        /// Entity'yi kalıcı olarak sil
        /// </summary>
        public virtual async Task<bool> HardDeleteAsync(Guid id)
        {
            try
            {
                var entity = await _dbSet.FindAsync(id);
                if (entity == null)
                {
                    return false;
                }

                _dbSet.Remove(entity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Entity of type {EntityType} hard deleted with ID {Id}", typeof(T).Name, id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <summary>
        /// Entity'nin var olup olmadığını kontrol et
        /// </summary>
        public virtual async Task<bool> ExistsAsync(Guid id)
        {
            try
            {
                return await _dbSet.AnyAsync(e => e.Id == id && e.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <summary>
        /// Toplam entity sayısını getir
        /// </summary>
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            try
            {
                var query = _dbSet.Where(e => e.IsActive);

                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                return await query.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting entities of type {EntityType}", typeof(T).Name);
                throw;
            }
        }
    }
}
