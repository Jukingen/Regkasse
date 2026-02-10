using System.Linq.Expressions;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Data.Repositories
{
    /// <summary>
    /// Generic repository pattern için temel interface
    /// </summary>
    /// <typeparam name="T">Entity tipi</typeparam>
    public interface IGenericRepository<T> where T : class, IEntity
    {
        /// <summary>
        /// Tüm entity'leri getir
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();
        
        /// <summary>
        /// Filtrelenmiş entity'leri getir
        /// </summary>
        Task<IEnumerable<T>> GetAsync(Expression<Func<T, bool>> predicate);
        
        /// <summary>
        /// Sayfalama ile entity'leri getir
        /// </summary>
        Task<(IEnumerable<T> Items, int TotalCount)> GetPagedAsync(
            int pageNumber, 
            int pageSize, 
            Expression<Func<T, bool>>? predicate = null,
            Expression<Func<T, object>>? orderBy = null,
            bool ascending = true);
        
        /// <summary>
        /// ID'ye göre entity getir
        /// </summary>
        Task<T?> GetByIdAsync(Guid id);
        
        /// <summary>
        /// Entity ekle
        /// </summary>
        Task<T> AddAsync(T entity);
        
        /// <summary>
        /// Entity güncelle
        /// </summary>
        Task<T> UpdateAsync(T entity);
        
        /// <summary>
        /// Entity sil (soft delete)
        /// </summary>
        Task<bool> DeleteAsync(Guid id);
        
        /// <summary>
        /// Entity'yi kalıcı olarak sil
        /// </summary>
        Task<bool> HardDeleteAsync(Guid id);
        
        /// <summary>
        /// Entity'nin var olup olmadığını kontrol et
        /// </summary>
        Task<bool> ExistsAsync(Guid id);
        
        /// <summary>
        /// Toplam entity sayısını getir
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    }
}
